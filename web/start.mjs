// "npm start": runs "ng serve" on the port Aspire assigns (PORT), or on 4200 without Aspire.
// A script instead of "ng serve --port $PORT" because that syntax differs between Windows and Linux shells.
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const port = Number.parseInt(process.env['PORT'] ?? '4200', 10);
if (!Number.isInteger(port) || port <= 0) {
  console.error(`Invalid PORT: ${process.env['PORT']}`);
  process.exit(1);
}

const ng = fileURLToPath(new URL('./node_modules/@angular/cli/bin/ng.js', import.meta.url));
const child = spawn(process.execPath, [ng, 'serve', '--port', String(port)], { stdio: 'inherit' });

// Stop "ng serve" when Aspire (or Ctrl+C) stops this script, so it does not keep the port busy
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => child.kill(signal));
child.on('exit', (code) => process.exit(code ?? 0));
