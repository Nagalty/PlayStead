ALTER TABLE process_signature_learning ADD COLUMN absence_baseline_established INTEGER NOT NULL DEFAULT 0
    CHECK (absence_baseline_established IN (0, 1));
