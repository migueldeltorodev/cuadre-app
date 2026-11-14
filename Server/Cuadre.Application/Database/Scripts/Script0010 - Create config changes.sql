-- RF-DAT-08: the changes made in Configuración (D-59). Rows are only ever inserted.
-- Voids, reopened days, price changes, Profit First plans and pay schemes are NOT copied here: their own tables already
-- keep them and are never edited, so the audit log reads them where they are.

CREATE TABLE config_changes (
    id            TEXT    NOT NULL PRIMARY KEY,
    changed_at    TEXT    NOT NULL,
    -- Settings, CashBox, PaymentMethod, ExchangeRate, Envelope, Employee, ExpenseCategory.
    -- No CHECK on purpose (see Script0003): the C# enum is the source of truth.
    subject       TEXT    NOT NULL,
    -- Null for the single settings row and for a rate (keyed by its date).
    subject_id    TEXT,
    -- What it was called WHEN it changed: a later rename must not rewrite the history.
    subject_name  TEXT    NOT NULL,
    -- JSON: [{"field": "Recargo por defecto", "before": "20 %", "after": "25 %"}], written for people.
    changes       TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_config_changes_changed_at ON config_changes (changed_at);
