# Changelog

本文件记录面向用户与分发者的版本变更。格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)。

## [Unreleased]

## [1.0.11] - 2026-08-14

### Added

- GitHub 仓库托管与 Velopack 在线更新默认源（`jixiangzhou007/MusicVault`）。
- 发现页浏览中枢（热门榜单 1–2 行横向铺满、推荐歌单墙）。
- 发布流水线：`velopack-release.ps1`、`publish-github-release.ps1`、tag 触发 Release Actions。

### Fixed

- 内置 API / 网易 API 端口占用与就绪竞态导致榜单为空的问题。

## [1.0.10] - 2026-05-24

### Added

- 艺术家详情：进入页读库内缓存；「刷新资料」才请求网络。
- 艺术家头像优先百度百科词条主图。
- 网易云官方专辑目录过滤（单曲盘、现场、错配歌手等）。
- 刷新资料时简介与专辑并行加载，先完成先展示。

### Fixed

- 艺术家刷新竞态导致专辑列表被清空的问题。
- 非百度百科来源的简介在详情页不显示的问题。

## [1.0.0] - 2026-04

### Added

- 初始公开发布：曲库扫描、多源发现与下载、元数据编辑、播放与歌词、Velopack 更新、Windows 单文件发布脚本。

---

版本号以 `Directory.Build.props` 中的 `Version` 为准。发布前请同步更新本文件与 `src/MusicVault.App/Data/release-notes.json`（若使用）。
