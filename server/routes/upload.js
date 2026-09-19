module.exports = (() => {
  const r = require('express').Router();
  const path = require('path');
  const fs = require('fs');
  const multer = require('multer');
  const dir = path.join(__dirname, '..', '..', 'uploads');
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  const storage = multer.diskStorage({
    destination: (req, file, cb) => cb(null, dir),
    filename: (req, file, cb) => {
      const ext = path.extname(Buffer.from(file.originalname, 'latin1').toString('utf8'));
      cb(null, Date.now() + '-' + Math.random().toString(36).slice(2, 8) + ext);
    }
  });
  const upload = multer({ storage, limits: { fileSize: 200 * 1024 * 1024 } });

  r.post('/', upload.single('file'), (req, res) => {
    if (!req.file) return res.status(400).json({ error: '未收到文件' });
    res.json({ url: '/uploads/' + req.file.filename, name:
      Buffer.from(req.file.originalname, 'latin1').toString('utf8'), size: req.file.size });
  });
  r.post('/multi', upload.array('files', 10), (req, res) => {
    res.json((req.files || []).map(f => ({ url: '/uploads/' + f.filename,
      name: Buffer.from(f.originalname, 'latin1').toString('utf8'), size: f.size })));
  });
  return r;
})();
