CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY,
    unity_player_id varchar(128) NOT NULL UNIQUE,
    name varchar(120) NOT NULL,
    email varchar(320) NOT NULL DEFAULT '',
    company_name varchar(180) NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS addresses (
    id uuid PRIMARY KEY,
    zonecode varchar(32) NOT NULL DEFAULT '',
    address varchar(500) NOT NULL,
    road_address varchar(500) NOT NULL DEFAULT '',
    jibun_address varchar(500) NOT NULL DEFAULT '',
    building_name varchar(240) NOT NULL DEFAULT '',
    bname varchar(120) NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_addresses_identity
ON addresses (zonecode, address, road_address, jibun_address);

CREATE TABLE IF NOT EXISTS user_addresses (
    unity_player_id varchar(128) NOT NULL,
    address_id uuid NOT NULL REFERENCES addresses (id) ON DELETE CASCADE,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (unity_player_id, address_id)
);

CREATE INDEX IF NOT EXISTS ix_user_addresses_address_id
ON user_addresses (address_id);

CREATE TABLE IF NOT EXISTS maps (
    id uuid PRIMARY KEY,
    address_id uuid NOT NULL REFERENCES addresses (id) ON DELETE RESTRICT,
    space_name varchar(160) NOT NULL,
    created_at timestamptz NOT NULL,
    scan_created_at timestamptz NULL,
    reconstruction_scan_id varchar(256) NOT NULL DEFAULT '',
    reconstruction_state varchar(32) NOT NULL DEFAULT '',
    reconstruction_message varchar(1000) NOT NULL DEFAULT '',
    reconstruction_result_file varchar(500) NOT NULL DEFAULT '',
    reconstruction_updated_at timestamptz NULL
);

ALTER TABLE maps
ALTER COLUMN scan_created_at DROP NOT NULL;

ALTER TABLE maps
ADD COLUMN IF NOT EXISTS reconstruction_scan_id varchar(256) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_state varchar(32) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_message varchar(1000) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_result_file varchar(500) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_updated_at timestamptz NULL;

UPDATE maps
SET scan_created_at = NULL
WHERE reconstruction_scan_id = '';

CREATE INDEX IF NOT EXISTS ix_maps_address_id
ON maps (address_id);

CREATE TABLE IF NOT EXISTS map_members (
    map_id uuid NOT NULL REFERENCES maps (id) ON DELETE CASCADE,
    unity_player_id varchar(128) NOT NULL,
    role varchar(32) NOT NULL,
    PRIMARY KEY (map_id, unity_player_id)
);

CREATE INDEX IF NOT EXISTS ix_map_members_unity_player_id
ON map_members (unity_player_id);

CREATE TABLE IF NOT EXISTS memos (
    id uuid PRIMARY KEY,
    map_id uuid NOT NULL REFERENCES maps (id) ON DELETE CASCADE,
    kind varchar(32) NOT NULL,
    urgency varchar(32) NOT NULL,
    title varchar(240) NOT NULL,
    body varchar(4000) NOT NULL DEFAULT '',
    author_unity_player_id varchar(128) NOT NULL,
    assignee_unity_player_id varchar(128) NOT NULL DEFAULT '',
    assignee_name varchar(120) NOT NULL DEFAULT '',
    work_status varchar(32) NOT NULL DEFAULT 'active',
    due_text varchar(80) NOT NULL DEFAULT '',
    has_spatial_anchor boolean NOT NULL DEFAULT false,
    reconstruction_scan_id varchar(256) NOT NULL DEFAULT '',
    position_x double precision NOT NULL DEFAULT 0,
    position_y double precision NOT NULL DEFAULT 0,
    position_z double precision NOT NULL DEFAULT 0,
    rotation_x double precision NOT NULL DEFAULT 0,
    rotation_y double precision NOT NULL DEFAULT 0,
    rotation_z double precision NOT NULL DEFAULT 0,
    rotation_w double precision NOT NULL DEFAULT 1,
    checklist_items jsonb NOT NULL DEFAULT '[]'::jsonb,
    voice_items jsonb NOT NULL DEFAULT '[]'::jsonb,
    image_urls jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    deleted_at timestamptz NULL
);

ALTER TABLE memos
ADD COLUMN IF NOT EXISTS deleted_at timestamptz NULL,
ADD COLUMN IF NOT EXISTS work_status varchar(32) NOT NULL DEFAULT 'active',
ADD COLUMN IF NOT EXISTS has_spatial_anchor boolean NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS reconstruction_scan_id varchar(256) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS position_x double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS position_y double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS position_z double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS rotation_x double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS rotation_y double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS rotation_z double precision NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS rotation_w double precision NOT NULL DEFAULT 1;

CREATE INDEX IF NOT EXISTS ix_memos_map_id_created_at
ON memos (map_id, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_memos_author_unity_player_id
ON memos (author_unity_player_id);

CREATE INDEX IF NOT EXISTS ix_memos_assignee_unity_player_id
ON memos (assignee_unity_player_id);

CREATE INDEX IF NOT EXISTS ix_memos_deleted_at
ON memos (deleted_at);

CREATE TABLE IF NOT EXISTS memo_reads (
    memo_id uuid NOT NULL REFERENCES memos (id) ON DELETE CASCADE,
    unity_player_id varchar(128) NOT NULL,
    read_at timestamptz NOT NULL,
    PRIMARY KEY (memo_id, unity_player_id)
);

CREATE INDEX IF NOT EXISTS ix_memo_reads_unity_player_id
ON memo_reads (unity_player_id);
