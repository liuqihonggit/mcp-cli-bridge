#!/usr/bin/env node

const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

// 获取 publish 目录路径
const publishDir = path.join(__dirname, 'publish');
const exePath = path.join(publishDir, 'McpHost.exe');

// 检查可执行文件是否存在
if (!fs.existsSync(exePath)) {
  console.error(`Error: McpHost.exe not found at ${exePath}`);
  console.error('Please ensure the package is properly installed.');
  process.exit(1);
}

// 检查 MemoryCli 是否存在
const memoryCliPath = path.join(publishDir, 'MemoryCli.exe');
if (!fs.existsSync(memoryCliPath)) {
  console.error(`Warning: MemoryCli.exe not found at ${memoryCliPath}`);
}

// 启动 MCP-CLI Bridge
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
