-- Slice 7: gastos (RF-GAS-01..04). An expense is money out of a cash box and an envelope (Operación by default,
-- never Reposición: D-01). Saving it confirms it; never edited, only voided with a reason (reversal dated today).

-- RF-GAS-03: the user's own list. Never deleted (expenses point to them), only deactivated.
CREATE TABLE expense_categories (
    id         TEXT    NOT NULL PRIMARY KEY,
    name       TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    is_active  INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at TEXT    NOT NULL,
    updated_at TEXT    NOT NULL
) STRICT;

-- RF-GAS-01: a template the app proposes every period (the owner confirms or skips each occurrence).
-- Monthly: due_day = day of the month (1-28, so every month has it). Weekly: due_day = DayOfWeek (0 = Sunday).
CREATE TABLE recurring_expenses (
    id           TEXT    NOT NULL PRIMARY KEY,
    name         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    category_id  TEXT    NOT NULL REFERENCES expense_categories (id),
    amount_cents INTEGER NOT NULL CHECK (amount_cents > 0),
    cash_box_id  TEXT    NOT NULL REFERENCES cash_boxes (id),
    envelope_id  TEXT    NOT NULL REFERENCES envelopes (id),
    -- Monthly, Weekly. No CHECK on purpose (see Script0003).
    frequency    TEXT    NOT NULL,
    due_day      INTEGER NOT NULL CHECK (due_day BETWEEN 0 AND 28),
    starts_on    TEXT    NOT NULL CHECK (starts_on GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    notes        TEXT,
    is_active    INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at   TEXT    NOT NULL,
    updated_at   TEXT    NOT NULL
) STRICT;

CREATE TABLE expenses (
    id                   TEXT    NOT NULL PRIMARY KEY,
    number               INTEGER NOT NULL UNIQUE CHECK (number > 0),
    date                 TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    category_id          TEXT    NOT NULL REFERENCES expense_categories (id),
    description          TEXT    NOT NULL,
    -- In the currency of the cash box.
    amount_cents         INTEGER NOT NULL CHECK (amount_cents > 0),
    cash_box_id          TEXT    NOT NULL REFERENCES cash_boxes (id),
    envelope_id          TEXT    NOT NULL REFERENCES envelopes (id),
    notes                TEXT,
    -- A confirmed occurrence of a recurring expense: the first day of its period (month, or the Monday of its week).
    recurring_expense_id TEXT    REFERENCES recurring_expenses (id),
    period_start         TEXT,
    status               TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    void_reason          TEXT,
    voided_at            TEXT,
    created_at           TEXT    NOT NULL,
    updated_at           TEXT    NOT NULL,
    CHECK ((status = 'Voided') = (void_reason IS NOT NULL)),
    CHECK ((recurring_expense_id IS NULL) = (period_start IS NULL))
) STRICT;

CREATE INDEX ix_expenses_date ON expenses (date);
CREATE INDEX ix_expenses_category ON expenses (category_id);

-- One confirmed expense per occurrence. Voiding it frees the period, so it is proposed again.
CREATE UNIQUE INDEX ux_expenses_recurring_period ON expenses (recurring_expense_id, period_start)
    WHERE recurring_expense_id IS NOT NULL AND status = 'Confirmed';

-- "This period it was not paid" (vacations, the gestor did not charge...): stops proposing that occurrence.
CREATE TABLE recurring_expense_skips (
    recurring_expense_id TEXT NOT NULL REFERENCES recurring_expenses (id),
    period_start         TEXT NOT NULL,
    created_at           TEXT NOT NULL,
    PRIMARY KEY (recurring_expense_id, period_start)
) STRICT;

-- The cash ledger learns the expense behind its rows.
ALTER TABLE cash_movements ADD COLUMN expense_id TEXT REFERENCES expenses (id);
CREATE INDEX ix_cash_movements_expense ON cash_movements (expense_id);
