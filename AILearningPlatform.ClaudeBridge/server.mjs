// Claude-Bridge: OpenAI-kompatibler Endpunkt (/v1/chat/completions) auf Basis des Claude Agent SDK.
// ApiServer und EvaluationWorker sprechen damit wie mit LM Studio. Nur LmStudio:BaseUrl umstellen.
// Auth: lokaler `claude login` ODER ANTHROPIC_API_KEY (wird vom SDK automatisch erkannt).
import http from 'node:http';
import { query } from '@anthropic-ai/claude-agent-sdk';

const PORT = Number(process.env.PORT ?? 1235);
const DEFAULT_MODEL = process.env.CLAUDE_MODEL ?? 'claude-sonnet-5-5';

const text = (c) =>
  typeof c === 'string' ? c : Array.isArray(c) ? c.map((p) => p.text ?? '').join('') : '';

async function complete(messages, requestedModel) {
  const system = messages.filter((m) => m.role === 'system').map((m) => text(m.content)).join('\n\n');
  const prompt = messages
    .filter((m) => m.role !== 'system')
    .map((m) => (m.role === 'assistant' ? `Assistant: ${text(m.content)}` : text(m.content)))
    .join('\n\n');

  // Nur Text-Antworten: keine Tools, ein Turn, keine Projekt-/User-Settings, keine Session-Dateien.
  const options = {
    systemPrompt: system || undefined,
    maxTurns: 1,
    tools: [],
    settingSources: [],
    persistSession: false,
    model: requestedModel && requestedModel.startsWith('claude-') ? requestedModel : DEFAULT_MODEL,
  };

  for await (const msg of query({ prompt, options })) {
    if (msg.type === 'result') {
      if (msg.subtype === 'success' && !msg.is_error) return { content: msg.result, model: options.model };
      throw new Error(`Claude-Fehler: ${msg.subtype === 'success' ? msg.result : msg.subtype}`);
    }
  }
  throw new Error('Keine Antwort von Claude erhalten');
}

http
  .createServer(async (req, res) => {
    const send = (code, body) => {
      res.writeHead(code, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify(body));
    };
    if (req.method === 'GET' && req.url === '/health') return send(200, { status: 'ok' });
    if (req.method === 'GET' && req.url === '/')
      return send(200, {
        service: 'claude-bridge',
        model: DEFAULT_MODEL,
        endpoints: ['POST /v1/chat/completions', 'GET /health'],
      });
    if (req.method !== 'POST' || req.url !== '/v1/chat/completions') return send(404, { error: 'not found' });

    try {
      let raw = '';
      for await (const chunk of req) raw += chunk;
      const body = JSON.parse(raw);
      const { content, model } = await complete(body.messages ?? [], body.model);
      send(200, {
        id: `chatcmpl-${Date.now()}`,
        object: 'chat.completion',
        created: Math.floor(Date.now() / 1000),
        model,
        choices: [{ index: 0, message: { role: 'assistant', content }, finish_reason: 'stop' }],
      });
    } catch (e) {
      console.error(e);
      send(500, { error: { message: String(e?.message ?? e) } });
    }
  })
  .listen(PORT, () => console.log(`Claude-Bridge läuft auf http://localhost:${PORT} (Modell: ${DEFAULT_MODEL})`));
