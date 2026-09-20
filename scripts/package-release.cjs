const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const root = path.resolve(__dirname, '..');
const target = path.resolve(process.argv[2] || path.join(root, 'work', 'release-' + new Date().toISOString().replace(/[:.]/g, '-')));
if (fs.existsSync(target) || fs.existsSync(target + '.tar.gz')) throw Error('Release destination already exists');
fs.mkdirSync(target, { recursive: true });
// Explicit allowlist excludes databases, uploads, credentials, caches and test output.
for (const name of ['server', 'shared', 'dist', 'public/sim', 'deploy', 'package.json', 'pnpm-lock.yaml', 'create-teacher.js', 'scripts/backup.cjs']) {
  const dest = path.join(target, name);
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.cpSync(path.join(root, name), dest, { recursive: true });
}
const files = [];
function scan(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const file = path.join(dir, entry.name);
    if (entry.isDirectory()) scan(file);
    else files.push({ path: path.relative(target, file).replaceAll('\\', '/'), bytes: fs.statSync(file).size, sha256: crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') });
  }
}
scan(target);
fs.writeFileSync(path.join(target, 'release-manifest.json'), JSON.stringify({ version: require('../package.json').version, created: new Date().toISOString(), files }, null, 2));
execFileSync('tar', ['-czf', target + '.tar.gz', '-C', target, '.']);
console.log(JSON.stringify({ archive: target + '.tar.gz', files: files.length, sha256: crypto.createHash('sha256').update(fs.readFileSync(target + '.tar.gz')).digest('hex') }));
