-- Slice 1: base configuration (RF-CFG-01/02/03/05/06/07, RF-CAJ-01, RF-MON-02).
-- Conventions: money = INTEGER cents, percentages = INTEGER basis points (30% = 3000),
-- ids = TEXT Guid, business dates = TEXT 'yyyy-MM-dd', timestamps = TEXT UTC ISO-8601 with 'Z'.
-- STRICT makes SQLite reject values of the wrong type instead of silently storing them.
-- Default rows are NOT inserted here: DatabaseSeeder does it (idempotent, reusable by the tests).

-- Single-row table: the CHECK on id guarantees there is only ever one settings row.
CREATE TABLE business_settings (
    id                               INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
    name                             TEXT    NOT NULL,
    address                          TEXT,
    -- Bit n set = DayOfWeek n is a closing day (Sunday = bit 0). 127 (every day) is not allowed.
    closed_days_mask                 INTEGER NOT NULL CHECK (closed_days_mask BETWEEN 0 AND 126),
    cash_difference_threshold_cents  INTEGER NOT NULL CHECK (cash_difference_threshold_cents >= 0),
    exchange_buffer_cents            INTEGER NOT NULL CHECK (exchange_buffer_cents >= 0),
    default_markup_bps               INTEGER NOT NULL CHECK (default_markup_bps >= 0),
    price_rounding_step_cents        INTEGER NOT NULL CHECK (price_rounding_step_cents >= 1),
    updated_at                       TEXT    NOT NULL
) STRICT;

-- Where the money physically is. Currency and kind are immutable once created.
CREATE TABLE cash_boxes (
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    currency    TEXT    NOT NULL CHECK (currency IN ('CUP', 'USD')),
    kind        TEXT    NOT NULL CHECK (kind IN ('Cash', 'Bank')),
    is_active   INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
) STRICT;

-- How customers pay. Each method lands in exactly one cash box (RF-CAJ-02).
CREATE TABLE payment_methods (
    id           TEXT    NOT NULL PRIMARY KEY,
    name         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    cash_box_id  TEXT    NOT NULL REFERENCES cash_boxes (id),
    is_active    INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at   TEXT    NOT NULL,
    updated_at   TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_payment_methods_cash_box_id ON payment_methods (cash_box_id);

-- One informal USD->CUP rate per calendar day (elTOQUE reference, typed by hand).
CREATE TABLE exchange_rates (
    date               TEXT    NOT NULL PRIMARY KEY
                               CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    cup_per_usd_cents  INTEGER NOT NULL CHECK (cup_per_usd_cents > 0),
    created_at         TEXT    NOT NULL,
    updated_at         TEXT    NOT NULL
) STRICT, WITHOUT ROWID;
