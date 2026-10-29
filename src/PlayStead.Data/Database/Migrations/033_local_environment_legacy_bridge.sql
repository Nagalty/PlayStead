PRAGMA foreign_keys=ON;

-- Preserve the legacy rows and project their trustworthy data into the
-- installation-scoped model. The UNIQUE index makes this safe to rerun.
INSERT OR IGNORE INTO local_environment_entries
    (entry_id, installation_id, kind, path, path_kind, confidence, source,
     exists_flag, last_observed_utc, last_modified_utc, metadata_json, is_user_override)
SELECT lower(hex(randomblob(16))), i.installation_id,
       CASE b.artifact_kind WHEN 0 THEN 1 WHEN 1 THEN 0 WHEN 2 THEN 2 ELSE 4 END,
       b.artifact_identity, 2, 1, 1, 0, b.captured_at_utc, NULL, NULL, 0
FROM local_artifact_baselines b
JOIN installations i ON i.game_id = b.game_id;

INSERT OR IGNORE INTO local_environment_entries
    (entry_id, installation_id, kind, path, path_kind, confidence, source,
     exists_flag, last_observed_utc, last_modified_utc, metadata_json, is_user_override)
SELECT lower(hex(randomblob(16))), i.installation_id,
       CASE u.artifact_kind WHEN 0 THEN 1 WHEN 1 THEN 0 WHEN 2 THEN 2 ELSE 4 END,
       u.path, 1, 2, 4, 0, u.created_at_utc, NULL,
       json_object('DisplayName', COALESCE(u.display_name, '')), 1
FROM user_defined_local_artifacts u
JOIN installations i ON i.game_id = u.game_id;

INSERT OR IGNORE INTO local_environment_baselines(installation_id, created_utc, updated_utc)
SELECT i.installation_id, MIN(b.captured_at_utc), MAX(b.captured_at_utc)
FROM local_artifact_baselines b
JOIN installations i ON i.game_id = b.game_id
GROUP BY i.installation_id;

INSERT OR IGNORE INTO local_environment_baseline_entries
    (installation_id, kind, normalized_path, component_kind, component_name,
     path_kind, exists_flag, last_modified_utc, metadata_json)
SELECT i.installation_id,
       CASE b.artifact_kind WHEN 0 THEN 1 WHEN 1 THEN 0 WHEN 2 THEN 2 ELSE 4 END,
       b.artifact_identity, '', '', 2, 0, b.captured_at_utc,
       json_object('FingerprintAlgorithm', b.algorithm, 'FingerprintHash', b.hash,
                   'FileCount', CAST(b.file_count AS TEXT), 'TotalSizeBytes', CAST(b.total_size_bytes AS TEXT))
FROM local_artifact_baselines b
JOIN installations i ON i.game_id = b.game_id;

INSERT OR IGNORE INTO local_file_snapshots
    (snapshot_id, installation_id, environment_entry_id, original_path, created_utc,
     reason, source_last_modified_utc, source_size_bytes, sha256,
     snapshot_relative_path, is_pre_restore_backup, metadata_json)
SELECT s.snapshot_id, i.installation_id,
       (SELECT e.entry_id FROM local_environment_entries e
        WHERE e.installation_id = i.installation_id
          AND e.kind = CASE s.artifact_kind WHEN 0 THEN 1 WHEN 1 THEN 0 WHEN 2 THEN 2 ELSE 4 END
          AND e.path = s.rule_identity AND e.is_user_override = 0 LIMIT 1),
       s.rule_identity, s.created_at_utc, s.reason, NULL, s.total_size_bytes,
       s.fingerprint_hash, s.archive_path, 0, NULL
FROM local_artifact_snapshots s
JOIN installations i ON i.game_id = s.game_id
WHERE length(trim(s.archive_path)) > 0
  AND EXISTS (SELECT 1 FROM local_environment_entries e
              WHERE e.installation_id = i.installation_id
                AND e.kind = CASE s.artifact_kind WHEN 0 THEN 1 WHEN 1 THEN 0 WHEN 2 THEN 2 ELSE 4 END
                AND e.path = s.rule_identity AND e.is_user_override = 0);
