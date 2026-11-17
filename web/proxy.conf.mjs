// Dev server only: forwards API and SignalR calls to the .NET API, so the browser sees a single origin
// (as in production, where the API serves the Angular files itself).
// Under Aspire, WithReference(api) injects API_HTTP (and the older services__api__http__0);
// without Aspire, the API's default local port.
const target =
  process.env['API_HTTP'] ?? process.env['services__api__http__0'] ?? 'http://localhost:5220';

console.log(`[proxy] /api and /hubs -> ${target}`);

export default {
  '/api': { target, secure: false, changeOrigin: true },
  '/hubs': { target, secure: false, changeOrigin: true, ws: true },
};
