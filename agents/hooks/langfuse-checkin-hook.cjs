const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { pathToFileURL } = require('node:url');

// Install this wrapper in the main checkout's secrets folder before trusting it.
const common = execFileSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], {
  encoding: 'utf8',
}).trim();
const normalize = (dir) => process.platform === 'win32' ? path.resolve(dir).toLowerCase() : path.resolve(dir);
if (normalize(common) !== normalize(path.join(__dirname, '../.git'))) process.exit(0);

const credentials = Object.fromEntries(
  fs.readFileSync(path.join(__dirname, 'langfuse.env'), 'utf8').trim().split(/\r?\n/).map(line => line.split('=')),
);
if (!credentials.PUBLIC_KEY || !credentials.SECRET_KEY) throw new Error('Missing local Langfuse keys');
Object.assign(process.env, {
  TRACE_TO_LANGFUSE: 'true',
  LANGFUSE_CODEX_BASE_URL: 'http://127.0.0.1:3300',
  LANGFUSE_CODEX_PUBLIC_KEY: credentials.PUBLIC_KEY,
  LANGFUSE_CODEX_SECRET_KEY: credentials.SECRET_KEY,
  LANGFUSE_CODEX_USER_ID: 'checkin-local',
  LANGFUSE_CODEX_TAGS: 'checkin-codex',
});

const codexDir = process.env.CODEX_HOME || path.join(os.homedir(), '.codex');
const exporter = path.join(codexDir, 'plugins/cache/codex-observability-plugin/tracing/0.4.3/dist/index.mjs');
import(pathToFileURL(exporter).href).catch(() => {
  console.error('Could not load the local Langfuse tracing plugin (0.4.3).');
  process.exitCode = 1;
});
