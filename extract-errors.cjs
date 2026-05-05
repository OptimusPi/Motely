const fs = require('fs');
const report = JSON.parse(fs.readFileSync('eslint-report.json', 'utf8'));
const errors = [];
report.forEach(file => {
  file.messages.forEach(msg => {
    errors.push(`${file.filePath}:${msg.line}:${msg.column} - ${msg.ruleId}`);
  });
});
fs.writeFileSync('eslint-errors.txt', errors.join('\n'));
