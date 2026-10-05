-- 용의 귀환 · 계정과 캐릭터 저장
CREATE TABLE IF NOT EXISTS accounts (
  name     TEXT PRIMARY KEY,          -- 캐릭터 이름 = 아이디 (최대 8자)
  salt     TEXT NOT NULL,
  hash     TEXT NOT NULL,             -- PBKDF2-SHA256(비밀번호)
  created  INTEGER NOT NULL,
  fails    INTEGER NOT NULL DEFAULT 0,
  lock_until INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS sessions (
  tok      TEXT PRIMARY KEY,          -- SHA-256(접속 토큰)
  name     TEXT NOT NULL,
  created  INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS sessions_name ON sessions(name);
CREATE TABLE IF NOT EXISTS saves (
  name     TEXT PRIMARY KEY,
  data     TEXT NOT NULL,             -- 게임 저장 JSON
  updated  INTEGER NOT NULL
);
