# SolidCraft 繁體中文

這個分支 `zh-TW` 把 SolidCraft 的繁體中文介面補齊，並統一採用台灣機械設計業界常用的 CAD 用詞。

## 內容

| 檔案 | 說明 |
|---|---|
| `assets/locales/zh-TW.json` | 繁中翻譯，3,309 條全部完成 |
| `assets/locales/pending/operation-cut.json` | 新訊息 `ui.operation_cut`：特徵運算的 Cut（除料），與剪貼簿的 Cut（剪下）分開 |
| `crates/ui-egui/src/dialogs.rs` | 運算選項改用 `ui.operation_cut` |
| `zh-TW/glossary-zh-TW.json` | 名詞對照表，新增或修改翻譯時依此用詞 |
| `zh-TW/build-zh-TW.cmd` | Windows 一鍵編譯腳本 |

## 在 Windows 編譯

按兩下 `zh-TW\build-zh-TW.cmd`。腳本會在需要時安裝 VS C++ 編譯工具與 Rust，編譯後啟動
`target\release\solidcraft.exe`。進度與錯誤記錄在 `zh-TW\build.log`。

啟動後到「系統選項 → 一般 → 介面語言」選「繁體中文」。

## 同步原專案更新

```
git fetch upstream
git merge upstream/main
```

翻譯檔若衝突，以 upstream 的 `en.json` 為準補上新增的 id，再依名詞對照表翻譯。
