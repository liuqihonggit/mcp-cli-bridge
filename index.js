#!/usr/bin/env node

const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

const exePath = path.join(__dirname, 'McpHost.exe');

if (!fs.existsSync(exePath)) {
  console.error(`Error: McpHost.exe not found at ${exePath}`);
  console.error('Please ensure the package is properly installed.');
  process.exit(1);
}

const memoryCliPath = path.join(__dirname, 'MemoryCli.exe');
if (!fs.existsSync(memoryCliPath)) {
  console.error(`Warning: MemoryCli.exe not found at ${memoryCliPath}`);
}

const child = spawn(exePath, process.argv.slice(2), {
  stdio: 'inherit',
  env: process.env
});

child.on('exit', (code) => {
  process.exit(code);
});

child.on('error', (err) => {
  console.error(`Failed to start McpHost: ${err.message}`);
  process.exit(1);
});
