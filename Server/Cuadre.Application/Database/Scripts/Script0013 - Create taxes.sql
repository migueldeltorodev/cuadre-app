-- D-62: the taxes the business may have to pay, configurable. They are only a SUGGESTION of how much to set aside
-- (the accountant summary and the Impuestos envelope show it): Profit First keeps splitting exactly as before.
-- None by default. Never deleted: deactivated.
CREATE TABLE taxes (
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    rate_bps    INTEGER NOT NULL CHECK (rate_bps BETWEEN 1 AND 10000),
    -- Sales = over what was collected (A1); Profit = over the profit before taxes. No CHECK on purpose (see
    -- Script0003): the C# enum is the source of truth.
    base        TEXT    NOT NULL,
    is_active   INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
) STRICT;
