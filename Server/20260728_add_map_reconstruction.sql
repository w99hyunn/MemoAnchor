BEGIN;

ALTER TABLE public.maps
ALTER COLUMN scan_created_at DROP NOT NULL;

ALTER TABLE public.maps
ADD COLUMN IF NOT EXISTS reconstruction_scan_id varchar(256) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_state varchar(32) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_message varchar(1000) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_result_file varchar(500) NOT NULL DEFAULT '',
ADD COLUMN IF NOT EXISTS reconstruction_updated_at timestamptz NULL;

-- 기존 scan_created_at 값은 실제 재구성 완료 시각이 아니므로 초기화한다.
UPDATE public.maps
SET scan_created_at = NULL
WHERE reconstruction_scan_id = '';

COMMIT;

SELECT
    column_name,
    data_type,
    is_nullable,
    character_maximum_length,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'maps'
  AND column_name IN (
      'scan_created_at',
      'reconstruction_scan_id',
      'reconstruction_state',
      'reconstruction_message',
      'reconstruction_result_file',
      'reconstruction_updated_at'
  )
ORDER BY ordinal_position;
