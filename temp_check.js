const fs = require('fs');
const path = 'src/app/features/guias.component/guias.component.html';
const lines = fs.readFileSync(path, 'utf8').split('\n');
const stack = [];
for (let i = 0; i < lines.length; i++) {
  const line = lines[i].trim();
  if (/^@if\s*\(.*\)\s*\{$/.test(line)) {
    stack.push({type:'if', line:i+1, text: line});
  } else if (/^@for\s*\(.*\)\s*\{$/.test(line)) {
    stack.push({type:'for', line:i+1, text: line});
  } else if (line === '}') {
    if (!stack.length) {
      console.log('Unmatched closing brace at line', i+1);
    } else {
      const top = stack.pop();
      console.log('Closed', top.type, 'opened at line', top.line, '->', top.text, 'at line', i+1);
    }
  }
}
console.log('Remaining brace stack count=', stack.length);
stack.forEach(item => console.log(item.type, 'opened at', item.line, item.text));
