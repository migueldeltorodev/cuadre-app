-- Slice 2: catalog (RF-PRD-01..07, RF-PRD-09, RF-PRV-01, RN-13).
-- Quantities are INTEGER thousandths ("_milli"): 100 lb = 100000, 2.5 lb = 2500.
-- Cost, average purchase rate and stock are NOT here: they are born with purchases (slice 3)
-- and inventory (slice 4), each in its own migration.

-- Two levels: a category (parent_id NULL, has a code prefix like 'ARR') and its subcategories
-- (parent_id set, no prefix). The CHECK keeps both shapes from mixing.
CREATE TABLE categories (
    id           TEXT NOT NULL PRIMARY KEY,
    parent_id    TEXT REFERENCES categories (id),
    name         TEXT NOT NULL,
    code_prefix  TEXT UNIQUE COLLATE NOCASE
                      CHECK (code_prefix IS NULL OR length(code_prefix) BETWEEN 2 AND 4),
    created_at   TEXT NOT NULL,
    updated_at   TEXT NOT NULL,
    CHECK ((parent_id IS NULL) = (code_prefix IS NOT NULL))
) STRICT;

-- GOTCHA: UNIQUE (parent_id, name) would NOT stop two top-level categories with the same name,
-- because SQLite treats every NULL as distinct. COALESCE turns NULL into a comparable value.
CREATE UNIQUE INDEX ux_categories_parent_name ON categories (COALESCE(parent_id, ''), name COLLATE NOCASE);

CREATE TABLE suppliers (
    id            TEXT    NOT NULL PRIMARY KEY,
    name          TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    contact_name  TEXT,
    phone         TEXT,
    notes         TEXT,
    is_active     INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    created_at    TEXT    NOT NULL,
    updated_at    TEXT    NOT NULL
) STRICT;

-- A product is a variety/brand (D-05). Units have no CHECK on purpose: the C# enum is the source of truth,
-- and adding a unit later must not force a rebuild of this table (SQLite cannot ALTER a CHECK).
CREATE TABLE products (
    id                       TEXT    NOT NULL PRIMARY KEY,
    code                     TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    barcode                  TEXT,
    name                     TEXT    NOT NULL,
    brand                    TEXT,
    presentation             TEXT,
    category_id              TEXT    NOT NULL REFERENCES categories (id),
    subcategory_id           TEXT    REFERENCES categories (id),
    usual_supplier_id        TEXT    REFERENCES suppliers (id),
    purchase_unit            TEXT    NOT NULL,
    stock_unit               TEXT    NOT NULL,
    -- Stock units in ONE purchase unit, in thousandths: 1 sack = 100 lb -> 100000.
    conversion_factor_milli  INTEGER NOT NULL CHECK (conversion_factor_milli > 0),
    -- NULL = not priced yet. Once priced, it can change but never go back to NULL (service rule).
    sale_price_cents         INTEGER CHECK (sale_price_cents IS NULL OR sale_price_cents >= 0),
    -- Price as it was when the price list was last copied to the shop sign (RF-PRD-09).
    listed_price_cents       INTEGER,
    min_stock_milli          INTEGER NOT NULL DEFAULT 0 CHECK (min_stock_milli >= 0),
    shrinkage_tolerance_bps  INTEGER NOT NULL DEFAULT 0 CHECK (shrinkage_tolerance_bps BETWEEN 0 AND 10000),
    -- Own "colchón" / "recargo"; NULL = use the business default (business_settings).
    exchange_buffer_cents    INTEGER CHECK (exchange_buffer_cents IS NULL OR exchange_buffer_cents >= 0),
    markup_bps               INTEGER CHECK (markup_bps IS NULL OR markup_bps >= 0),
    is_perishable            INTEGER NOT NULL DEFAULT 0 CHECK (is_perishable IN (0, 1)),
    is_active                INTEGER NOT NULL DEFAULT 1 CHECK (is_active IN (0, 1)),
    notes                    TEXT,
    created_at               TEXT    NOT NULL,
    updated_at               TEXT    NOT NULL
) STRICT;

-- Barcode is optional, but unique when present (partial index).
CREATE UNIQUE INDEX ux_products_barcode ON products (barcode) WHERE barcode IS NOT NULL;
CREATE INDEX ix_products_category_id ON products (category_id);
CREATE INDEX ix_products_subcategory_id ON products (subcategory_id);
CREATE INDEX ix_products_usual_supplier_id ON products (usual_supplier_id);

-- Sale price history (RF-PRD-05). Rows are only ever inserted.
CREATE TABLE product_price_changes (
    id                    TEXT    NOT NULL PRIMARY KEY,
    product_id            TEXT    NOT NULL REFERENCES products (id),
    price_cents           INTEGER NOT NULL CHECK (price_cents >= 0),
    previous_price_cents  INTEGER,
    effective_date        TEXT    NOT NULL,
    reason                TEXT    NOT NULL CHECK (reason IN ('Manual', 'Purchase')),
    created_at            TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_product_price_changes_product ON product_price_changes (product_id, created_at);

-- Each time the price list was copied by hand to the shop sign.
CREATE TABLE price_list_copies (
    id             TEXT    NOT NULL PRIMARY KEY,
    copied_at      TEXT    NOT NULL,
    product_count  INTEGER NOT NULL,
    changed_count  INTEGER NOT NULL
) STRICT;
