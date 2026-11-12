-- Slice 3: purchases (RF-CMP-01..07, RF-PRV-02), plus the minimal base slice 4 (inventory) builds on.
-- Units: quantities in thousandths ("_milli"), money in cents, unit COSTS in millionths ("_micros"):
-- 0.62 USD per lb = 620000 micros. Cents are too coarse for an average that keeps moving (0.615 → 0.62).
-- "kind" columns of the ledgers have no CHECK on purpose: later slices add kinds (Transfer, Exit, Sale...)
-- and SQLite cannot ALTER a CHECK without rebuilding the table. The C# enums are the source of truth.

-- Where the goods are (RF-INV-01). Seeded by DatabaseSeeder: Almacén (Storage) and Mostrador (Counter).
CREATE TABLE locations (
    id          TEXT    NOT NULL PRIMARY KEY,
    name        TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    kind        TEXT    NOT NULL CHECK (kind IN ('Storage', 'Counter')),
    is_active   INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL
) STRICT;

-- A confirmed purchase is never edited: it is voided with a reason (RF-CMP-07).
CREATE TABLE purchases (
    id                      TEXT    NOT NULL PRIMARY KEY,
    number                  INTEGER NOT NULL UNIQUE CHECK (number > 0),
    date                    TEXT    NOT NULL CHECK (date GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    supplier_id             TEXT    NOT NULL REFERENCES suppliers (id),
    currency                TEXT    NOT NULL CHECK (currency IN ('CUP', 'USD')),
    -- The rate used for THIS purchase (prefilled with the day's elTOQUE rate, editable).
    rate_cup_per_usd_cents  INTEGER NOT NULL CHECK (rate_cup_per_usd_cents > 0),
    location_id             TEXT    NOT NULL REFERENCES locations (id),
    -- Where the money for the goods came from. Charges carry their own box.
    cash_box_id             TEXT    NOT NULL REFERENCES cash_boxes (id),
    goods_total_cents       INTEGER NOT NULL CHECK (goods_total_cents > 0),
    notes                   TEXT,
    status                  TEXT    NOT NULL CHECK (status IN ('Confirmed', 'Voided')),
    void_reason             TEXT,
    voided_at               TEXT,
    created_at              TEXT    NOT NULL,
    updated_at              TEXT    NOT NULL,
    CHECK ((status = 'Voided') = (void_reason IS NOT NULL))
) STRICT;

CREATE INDEX ix_purchases_date ON purchases (date);
CREATE INDEX ix_purchases_supplier_id ON purchases (supplier_id);

-- Units and factor are a SNAPSHOT: editing the product later must not change what an old purchase meant.
CREATE TABLE purchase_lines (
    id                            TEXT    NOT NULL PRIMARY KEY,
    purchase_id                   TEXT    NOT NULL REFERENCES purchases (id),
    line_no                       INTEGER NOT NULL,
    product_id                    TEXT    NOT NULL REFERENCES products (id),
    purchase_unit                 TEXT    NOT NULL,
    stock_unit                    TEXT    NOT NULL,
    quantity_milli                INTEGER NOT NULL CHECK (quantity_milli > 0),          -- purchase units
    conversion_factor_milli       INTEGER NOT NULL CHECK (conversion_factor_milli > 0),
    stock_quantity_milli          INTEGER NOT NULL CHECK (stock_quantity_milli > 0),    -- stock units
    unit_cost_cents               INTEGER NOT NULL CHECK (unit_cost_cents > 0),         -- purchase currency, per purchase unit
    line_total_cents              INTEGER NOT NULL CHECK (line_total_cents > 0),
    allocated_charges_usd_micros  INTEGER NOT NULL CHECK (allocated_charges_usd_micros >= 0),
    -- Landed cost (goods + its share of the charges) per STOCK unit.
    landed_unit_cost_usd_micros   INTEGER NOT NULL CHECK (landed_unit_cost_usd_micros > 0),
    landed_unit_cost_cup_micros   INTEGER NOT NULL CHECK (landed_unit_cost_cup_micros > 0),
    UNIQUE (purchase_id, line_no),
    UNIQUE (purchase_id, product_id)
) STRICT;

CREATE INDEX ix_purchase_lines_product_id ON purchase_lines (product_id);

-- Freight, unloading... (RF-CMP-02). Spread over the lines by value; each paid from its own box.
CREATE TABLE purchase_charges (
    id            TEXT    NOT NULL PRIMARY KEY,
    purchase_id   TEXT    NOT NULL REFERENCES purchases (id),
    description   TEXT    NOT NULL,
    currency      TEXT    NOT NULL CHECK (currency IN ('CUP', 'USD')),
    amount_cents  INTEGER NOT NULL CHECK (amount_cents > 0),
    cash_box_id   TEXT    NOT NULL REFERENCES cash_boxes (id)
) STRICT;

CREATE INDEX ix_purchase_charges_purchase_id ON purchase_charges (purchase_id);

-- THE stock ledger (RF-INV-02): stock = SUM(quantity_milli), never typed by hand. Insert-only.
-- Incoming rows with a cost carry it per stock unit; the cost replay reads them in (date, created_at) order.
CREATE TABLE stock_movements (
    id                    TEXT    NOT NULL PRIMARY KEY,
    product_id            TEXT    NOT NULL REFERENCES products (id),
    location_id           TEXT    NOT NULL REFERENCES locations (id),
    date                  TEXT    NOT NULL,
    kind                  TEXT    NOT NULL,
    quantity_milli        INTEGER NOT NULL CHECK (quantity_milli <> 0),
    unit_cost_usd_micros  INTEGER,
    unit_cost_cup_micros  INTEGER,
    purchase_id           TEXT    REFERENCES purchases (id),
    purchase_line_id      TEXT    REFERENCES purchase_lines (id),
    created_at            TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_stock_movements_product ON stock_movements (product_id, date, created_at);
CREATE INDEX ix_stock_movements_location ON stock_movements (location_id);
CREATE INDEX ix_stock_movements_purchase ON stock_movements (purchase_id);

-- THE cash ledger: box balance = SUM(amount_cents), in the box's currency. Insert-only.
-- Envelopes (slice 5) will hang from these same rows.
CREATE TABLE cash_movements (
    id            TEXT    NOT NULL PRIMARY KEY,
    cash_box_id   TEXT    NOT NULL REFERENCES cash_boxes (id),
    date          TEXT    NOT NULL,
    kind          TEXT    NOT NULL,
    amount_cents  INTEGER NOT NULL CHECK (amount_cents <> 0),
    description   TEXT    NOT NULL,
    purchase_id   TEXT    REFERENCES purchases (id),
    created_at    TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_cash_movements_box ON cash_movements (cash_box_id, date);
CREATE INDEX ix_cash_movements_purchase ON cash_movements (purchase_id);

-- DERIVED cache of RN-01/RN-02, rebuilt by replaying stock_movements whenever a purchase is confirmed or voided.
-- Column names are unique across products + product_costs on purpose: product queries join both unprefixed.
CREATE TABLE product_costs (
    product_id           TEXT    NOT NULL PRIMARY KEY REFERENCES products (id),
    avg_cost_usd_micros  INTEGER NOT NULL CHECK (avg_cost_usd_micros > 0),
    avg_cost_cup_micros  INTEGER NOT NULL CHECK (avg_cost_cup_micros > 0),
    last_purchase_date   TEXT,
    recalculated_at      TEXT    NOT NULL
) STRICT, WITHOUT ROWID;
