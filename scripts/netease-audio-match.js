#!/usr/bin/env node
/**
 * MusicVault: 音频文件 → 网易云 audioFP → 本地 /audio/match
 * 用法: node netease-audio-match.js <pcm-f32le-path> <api-base> [durationSec]
 * stdin/stdout: 最后一行 stdout 为 JSON { ok, message?, artist?, title?, keyword?, confidence? }
 */
'use strict';

const fs = require('fs');
const path = require('path');

const pcmPath = process.argv[2];
const apiBase = (process.argv[3] || 'http://localhost:3000').replace(/\/$/, '');
const durationSec = Math.max(1, Math.min(20, Number(process.argv[4] || 8)));

function emit(payload) {
  process.stdout.write(JSON.stringify(payload));
}

function fail(message) {
  emit({ ok: false, message });
  process.exit(1);
}

if (!pcmPath || !fs.existsSync(pcmPath)) {
  fail('PCM 样本文件不存在');
}

function resolveAfpDir(startDir) {
  const roots = [startDir, path.join(startDir, '..'), path.join(startDir, '..', '..')];
  for (const root of roots) {
    const candidates = [
      path.join(root, 'node_modules', 'netease-cloud-music-api-alger', 'public', 'audio_match_demo'),
      path.join(root, 'public', 'audio_match_demo'),
    ];
    for (const dir of candidates) {
      if (fs.existsSync(path.join(dir, 'afp.js'))) return dir;
    }
  }
  return null;
}

const scriptDir = __dirname;
const apiDir = path.join(scriptDir, '..');
const afpDir = resolveAfpDir(apiDir) || resolveAfpDir(process.cwd());
if (!afpDir) {
  fail('未找到 audio_match_demo/afp.js（请确认已安装 netease-cloud-music-api-alger）');
}

const buf = fs.readFileSync(pcmPath);
if (buf.length < 4) {
  fail('PCM 样本过短');
}

const sampleCount = Math.min(Math.floor(durationSec * 8000), Math.floor(buf.length / 4));
const samples = new Float32Array(buf.buffer, buf.byteOffset, sampleCount);

const log = console.log;
console.log = (...args) => console.error('[afp]', ...args);

const { GenerateFP } = require(path.join(afpDir, 'afp.js'));

GenerateFP(samples)
  .then((fp) => {
    if (!fp) return fail('生成音频指纹失败');
    const url =
      `${apiBase}/audio/match?` +
      new URLSearchParams({ duration: String(durationSec), audioFP: fp }).toString();
    return fetch(url, { method: 'POST' });
  })
  .then((resp) => {
    if (!resp.ok) return fail(`网易云识曲 HTTP ${resp.status}`);
    return resp.json();
  })
  .then((body) => {
    const code = body?.code ?? body?.body?.code;
    if (code !== 200) {
      const msg = body?.message || body?.msg || body?.body?.message || '识曲失败';
      return fail(String(msg));
    }
    const data = body?.data ?? body?.body?.data;
    const list = data?.result ?? data?.songs ?? data?.list;
    if (!Array.isArray(list) || list.length === 0) {
      return fail('未识别到匹配歌曲');
    }
    const top = list[0];
    const song = top?.song ?? top;
    const title = song?.name || song?.title || '';
    const artistField = song?.ar ?? song?.artists ?? song?.artist;
    const artists = Array.isArray(artistField)
      ? artistField.map((a) => (typeof a === 'string' ? a : a?.name)).filter(Boolean).join('/')
      : (typeof artistField === 'string' ? artistField : song?.singer || '');
    if (!title) return fail('识曲结果缺少歌名');
    const keyword = artists && title ? `${artists}-${title}` : title;
    const rawScore = typeof top?.score === 'number' ? top.score : null;
    const confidence = rawScore == null ? null : (rawScore > 1 ? rawScore / 100 : rawScore);
    emit({
      ok: true,
      message: '识别成功',
      artist: artists,
      title,
      keyword,
      confidence,
    });
  })
  .catch((err) => fail(err?.message || String(err)))
  .finally(() => {
    console.log = log;
  });
