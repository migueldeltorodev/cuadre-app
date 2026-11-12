-- Slice 4: inventory (RF-INV-01..08). Transfers, non-sale exits and physical counts are "stock operations":
-- a document (header + lines) that writes its movements into the SAME stock ledger as purchases.
-- Like a purchase, an operation is never edited: it is voided with a reason and its movements are reversed.
-- Units: quantities in thousandths of the STOCK unit ("_milli"), unit costs in millionths ("_micros").

CREATE TABLE stock_operations (
    id               TEXT    NOT NULL PRIMARY KEY,
    number           INTEGER NOT NULL UNIQUE CHECK (number > 0),
    -- Transfer, Exit, Count. No CHECK on purpose (see Script0003): the C# enum is the source of truth.
    kind             TEXT    NOT NULL,
    date             TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    -- Transfer: origin. Exit: where the goods left from. Count: the location that was counted.
    location_id      TEXT    NOT NULL REFERENCES locations (id),
    -- Transfer only: destination.
    to_location_id   TEXT    REFERENCES locations (id),
    -- Exit only: Shrinkage, Expired, Damaged, HouseConsumption, Adjustment.
    exit_reason      TEXT,
    notes            TEXT,
    status           TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    void_reason      TEXT,
    voided_at        TEXT,
    created_at       TEXT    NOT NULL,
    updated_at       TEXT    NOT NULL,
    CHECK ((status = 'Voided') = (void_reason IS NOT NULL)),
    CHECK (to_location_id IS NULL OR to_location_id <> location_id)
) STRICT;

CREATE INDEX ix_stock_operations_date ON stock_operations (date);
CREATE INDEX ix_stock_operations_kind ON stock_operations (kind, date);

CREATE TABLE stock_operation_lines (
    id                        TEXT    NOT NULL PRIMARY KEY,
    operation_id              TEXT    NOT NULL REFERENCES stock_operations (id),
    line_no                   INTEGER NOT NULL,
    product_id                TEXT    NOT NULL REFERENCES products (id),
    -- What was typed, as typed: "2 sacos" stays 2000 + in_purchase_units = 1. Snapshot of the factor used.
    entered_quantity_milli    INTEGER NOT NULL CHECK (entered_quantity_milli >= 0),
    entered_in_purchase_units INTEGER NOT NULL CHECK (entered_in_purchase_units IN (0, 1)),
    conversion_factor_milli   INTEGER NOT NULL CHECK (conversion_factor_milli > 0),
    -- Transfer/Exit: the quantity moved (> 0). Count: counted − expected (may be 0 or negative).
    quantity_milli            INTEGER NOT NULL,
    -- Count only.
    expected_milli            INTEGER,
    counted_milli             INTEGER CHECK (counted_milli IS NULL OR counted_milli >= 0),
    -- Average cost at that moment (exits and count differences): values the loss (RF-REP-08) and the
    -- house consumption (RN-11). Null when the product was never bought.
    unit_cost_usd_micros      INTEGER,
    unit_cost_cup_micros      INTEGER,
    UNIQUE (operation_id, line_no),
    UNIQUE (operation_id, product_id)
) STRICT;

CREATE INDEX ix_stock_operation_lines_product_id ON stock_operation_lines (product_id);

-- The ledger learns where its new rows come from. Purchases keep using purchase_id.
ALTER TABLE stock_movements ADD COLUMN operation_id TEXT REFERENCES stock_operations (id);

CREATE INDEX ix_stock_movements_operation ON stock_movements (operation_id);
CREATE INDEX ix_stock_movements_date ON stock_movements (date);
