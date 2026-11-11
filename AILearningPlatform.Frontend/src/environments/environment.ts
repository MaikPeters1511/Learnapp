export const environment = {
  production: false,
  // Direkt der HTTPS-Port des ApiServers (launchSettings). Über http://localhost:5000 würde der Server per 307 auf
  // einen anderen Ursprung weiterleiten, und Browser entfernen dabei den Authorization-Header der Anmeldung.
  apiUrl: 'https://localhost:7022',
};
