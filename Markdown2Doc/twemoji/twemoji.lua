-- twemoji-all.lua
-- 將所有文字中的 emoji 轉為 <img class="emoji" src="...">（Twemoji PNG）
-- 特色：
-- * 支援 ZWJ、膚色、區旗、鍵帽、VS16/VS15、tag sequences
-- * 只有檔案存在才替換；路徑/副檔名可由 Meta 覆寫
--
-- Pandoc: Lua 5.3+ / utf8 可用

local UTF8 = utf8

-- ===== 設定（可被 Meta 覆寫） =====
local CFG = {
  base_dir = "twemoji/72x72",  -- 圖檔目錄（相對於目前工作目錄）
  ext = ".png",                    -- 副檔名 .png 或 .svg（wkhtmltopdf 建議 .png）
  class = "emoji",                 -- <img> class
  width = nil,                     -- 若要固定寬高，可填字串例如 "18"（像素）
  height = nil,
}

-- ===== 工具 =====
local function file_exists(path)
  local f = io.open(path, "rb")
  if f then f:close(); return true end
  return false
end

local function hex(n)
  return string.format("%x", n)
end

-- 將一串 codepoints 轉 Twemoji 檔名：codepoints 以 '-' 串接、全小寫十六進位
local function cps_to_twemoji_name(cps)
  local parts = {}
  for i = 1, #cps do parts[i] = hex(cps[i]) end
  return table.concat(parts, "-")
end

-- 讀 Meta 覆寫
function Meta(meta)
  if meta.twemoji_base_dir and meta.twemoji_base_dir.t == "MetaInlines" then
    CFG.base_dir = pandoc.utils.stringify(meta.twemoji_base_dir)
  end
  if meta.twemoji_ext and meta.twemoji_ext.t == "MetaInlines" then
    CFG.ext = pandoc.utils.stringify(meta.twemoji_ext)
  end
  if meta.twemoji_class and meta.twemoji_class.t == "MetaInlines" then
    CFG.class = pandoc.utils.stringify(meta.twemoji_class)
  end
  if meta.twemoji_width and meta.twemoji_width.t == "MetaInlines" then
    CFG.width = pandoc.utils.stringify(meta.twemoji_width)
  end
  if meta.twemoji_height and meta.twemoji_height.t == "MetaInlines" then
    CFG.height = pandoc.utils.stringify(meta.twemoji_height)
  end
end

-- ===== Emoji 相關判斷 & 取 cluster =====
-- 參考 UTS #51/Unicode：此處用「最長匹配」啟發式處理常見組合：
-- - Regional Indicators (旗)
-- - Keycap sequences (#, *, 0-9 + FE0F? + 20E3)
-- - ZWJ sequences (… 200D …)
-- - Emoji modifiers (膚色 1F3FB-1F3FF)
-- - Variation selectors FE0E/FE0F
-- - Tag sequences (1F3F4 + TAGS + 007F)
--
-- 重點：我們不嘗試「判定是不是 emoji」，而是先組最大可能簇，再嘗試映射到實際檔案，存在才替換。

local ZWJ = 0x200D
local VS15, VS16 = 0xFE0E, 0xFE0F
local COMBINING_ENCLOSING_KEYCAP = 0x20E3
local RI_FIRST, RI_LAST = 0x1F1E6, 0x1F1FF
local SKIN_TONE_FIRST, SKIN_TONE_LAST = 0x1F3FB, 0x1F3FF
local TAG_FIRST, TAG_LAST = 0xE0020, 0xE007E
local CANCEL_TAG = 0xE007F

local function is_regional_indicator(cp)
  return cp >= RI_FIRST and cp <= RI_LAST
end
local function is_skin_tone(cp)
  return cp >= SKIN_TONE_FIRST and cp <= SKIN_TONE_LAST
end
local function is_tag(cp)
  return cp >= TAG_FIRST and cp <= TAG_LAST
end

-- 讀取下一個 codepoint
local function next_cp(s, i)
  local cp = string.byte(s, i)
  if not cp then return nil, i end
  local c, n = utf8.codepoint(s, i), utf8.len(s)
  local j = utf8.offset(s, 2, i)
  if not j then
    -- 單一碼或尾端
    j = #s + 1
  end
  return c, j
end

-- 嘗試擷取「最長」emoji cluster，回傳：cluster(原始字串)、codepoints 陣列、下一個 index
local function read_emoji_cluster(s, i0)
  local cps, bytes = {}, {}
  local i = i0

  local function push(cp, j)
    cps[#cps+1] = cp
    bytes[#bytes+1] = s:sub(i, j-1)
    i = j
  end

  -- 先讀一個 cp
  local cp, j = next_cp(s, i)
  if not cp then return nil, nil, i0 end

  -- 旗：兩個 RI 連在一起
  if is_regional_indicator(cp) then
    local cp2, j2 = next_cp(s, j)
    if cp2 and is_regional_indicator(cp2) then
      push(cp, j); push(cp2, j2)
      return table.concat(bytes), cps, i
    end
  end

  -- 鍵帽： [#*0-9] + VS16? + 20E3
  local is_keycap_base = (cp == 0x23 or cp == 0x2A or (cp >= 0x30 and cp <= 0x39))
  if is_keycap_base then
    push(cp, j)
    local cp2, j2 = next_cp(s, i)
    if cp2 == VS16 then push(cp2, j2) end
    local cp3, j3 = next_cp(s, i)
    if cp3 == COMBINING_ENCLOSING_KEYCAP then
      push(cp3, j3)
      return table.concat(bytes), cps, i
    else
      -- 不是鍵帽序列，回退到單碼（下面繼續一般處理）
      i = i0; cps = {}; bytes = {}
      cp, j = next_cp(s, i)
    end
  end

  -- 一般情況：允許附帶 VS16/VS15、膚色、ZWJ 串接、Tag sequences
  push(cp, j)

  -- 允許 1 個 VS16/VS15
  local cp2, j2 = next_cp(s, i)
  if cp2 == VS15 or cp2 == VS16 then push(cp2, j2) end

  -- 允許 1 個膚色
  local cp3, j3 = next_cp(s, i)
  if cp3 and is_skin_tone(cp3) then push(cp3, j3) end

  -- ZWJ 串接（零個或多個）：... 200D [emoji (+VS/skin)] ...
  while true do
    local cpz, jz = next_cp(s, i)
    if cpz ~= ZWJ then break end
    push(cpz, jz)

    local cpn, jn = next_cp(s, i)
    if not cpn then break end
    push(cpn, jn)

    -- 接著給 VS / 膚色
    local cpa, ja = next_cp(s, i)
    if cpa == VS15 or cpa == VS16 then push(cpa, ja) end
    local cpb, jb = next_cp(s, i)
    if cpb and is_skin_tone(cpb) then push(cpb, jb) end
  end

  -- Tag sequences（像海盜旗 1F3F4 + TAGS + E007F）
  local cpt, jt = next_cp(s, i)
  if cps[1] == 0x1F3F4 and cpt and is_tag(cpt) then
    while cpt and is_tag(cpt) do
      push(cpt, jt)
      cpt, jt = next_cp(s, i)
    end
    if cpt == CANCEL_TAG then push(cpt, jt) end
  end

  return table.concat(bytes), cps, i
end

-- 嘗試把 cluster -> Twemoji 檔案路徑（若不存在則回 nil）
local file_cache = {} -- memoize: key=filename -> true/false

local function choose_twemoji_path(cps)
  -- 先用「移除 FE0E/FE0F」的版本試
  local filtered = {}
  for _, cp in ipairs(cps) do
    if cp ~= VS15 and cp ~= VS16 then
      filtered[#filtered+1] = cp
    end
  end
  local name1 = cps_to_twemoji_name(filtered)
  local path1 = CFG.base_dir .. "/" .. name1 .. CFG.ext

  local ok = file_cache[path1]
  if ok == nil then
    ok = file_exists(path1)
    file_cache[path1] = ok
  end
  if ok then return path1 end

  -- 找不到，再嘗試「包含 FE0E/FE0F」的原始版本（Twemoji 部分檔名包含 VS16）
  local name2 = cps_to_twemoji_name(cps)
  local path2 = CFG.base_dir .. "/" .. name2 .. CFG.ext
  local ok2 = file_cache[path2]
  if ok2 == nil then
    ok2 = file_exists(path2)
    file_cache[path2] = ok2
  end
  if ok2 then return path2 end

  return nil
end

-- 將一段文字拆為 Inlines，能自動替換 emoji 為 Image
local function str_to_inlines(s)
  local out = {}
  local i = 1
  local nbytes = #s
  while i <= nbytes do
    local cluster, cps, j = read_emoji_cluster(s, i)
    if not cluster then
      -- 安全起見（理論上不會進來）
      table.insert(out, pandoc.Str(s:sub(i, i)))
      i = i + 1
    else
      local path = choose_twemoji_path(cps or {})
      if path then
        local attr = { class = CFG.class }
        if CFG.width then attr.width = CFG.width end
        if CFG.height then attr.height = CFG.height end
        table.insert(out, pandoc.Image({pandoc.Str(cluster)}, path, "emoji", attr))
      else
        -- 非 emoji 或無對應檔案 -> 原字串直接塞回
        table.insert(out, pandoc.Str(cluster))
      end
      i = j
    end
  end
  return out
end

-- 處理各種行內容器
local function map_inlines(inlines)
  local r = {}
  for _, el in ipairs(inlines) do
    if el.t == "Str" then
      local parts = str_to_inlines(el.text)
      for _, p in ipairs(parts) do table.insert(r, p) end
    else
      table.insert(r, el)
    end
  end
  return r
end

-- 避免在 Code/CodeBlock/Math 等區域替換（通常不希望改動）
local function passthrough() end

return {
  { Meta = Meta },
  { Str = function(el) return map_inlines({el}) end },
  { Para = function(el) el.content = map_inlines(el.content); return el end },
  { Plain = function(el) el.content = map_inlines(el.content); return el end },
  { Header = function(el) el.content = map_inlines(el.content); return el end },
  { Table = function(el)
      -- 表格儲存格內的段落也會經過 Para/Plain，這裡通常不用特別處理
      return el
    end
  },
  { Code = passthrough, CodeBlock = passthrough, Math = passthrough },
}
