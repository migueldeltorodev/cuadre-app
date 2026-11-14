-- Polish: the suggested price and the colchón alert use the LAST purchase's landed cost (D-52, RN-03, RN-04);
-- the weighted average keeps valuing stock, losses and the reports. Shelf life of perishables (RF-PRD-10, D-53).

-- The cost cache is rebuilt with the last cost NOT NULL: SQLite cannot add a NOT NULL column without a default.
-- Last cost = the latest entry of a CONFIRMED purchase (positive count differences also carry a cost, but they
-- are not purchases). With no confirmed purchase left (all voided), it falls back to the average.
-- last_purchase_date is recomputed with the same rule (it used to count positive count differences too).
CREATE TABLE product_costs_new (
    product_id            TEXT    NOT NULL PRIMARY KEY REFERENCES products (id),
    avg_cost_usd_micros   INTEGER NOT NULL CHECK (avg_cost_usd_micros > 0),
    avg_cost_cup_micros   INTEGER NOT NULL CHECK (avg_cost_cup_micros > 0),
    last_cost_usd_micros  INTEGER NOT NULL CHECK (last_cost_usd_micros > 0),
    last_cost_cup_micros  INTEGER NOT NULL CHECK (last_cost_cup_micros > 0),
    last_purchase_date    TEXT,
    recalculated_at       TEXT    NOT NULL
) STRICT, WITHOUT ROWID;

INSERT INTO product_costs_new (
    product_id, avg_cost_usd_micros, avg_cost_cup_micros, last_cost_usd_micros, last_cost_cup_micros,
    last_purchase_date, recalculated_at)
SELECT
    pc.product_id,
    pc.avg_cost_usd_micros,
    pc.avg_cost_cup_micros,
    COALESCE((SELECT m.unit_cost_usd_micros
              FROM stock_movements m JOIN purchases p ON p.id = m.purchase_id
              WHERE m.product_id = pc.product_id AND m.quantity_milli > 0 AND p.status = 'Confirmed'
                AND m.unit_cost_usd_micros IS NOT NULL AND m.unit_cost_cup_micros IS NOT NULL
              ORDER BY m.date DESC, m.created_at DESC, m.rowid DESC
              LIMIT 1), pc.avg_cost_usd_micros),
    COALESCE((SELECT m.unit_cost_cup_micros
              FROM stock_movements m JOIN purchases p ON p.id = m.purchase_id
              WHERE m.product_id = pc.product_id AND m.quantity_milli > 0 AND p.status = 'Confirmed'
                AND m.unit_cost_usd_micros IS NOT NULL AND m.unit_cost_cup_micros IS NOT NULL
              ORDER BY m.date DESC, m.created_at DESC, m.rowid DESC
              LIMIT 1), pc.avg_cost_cup_micros),
    (SELECT MAX(m.date)
     FROM stock_movements m JOIN purchases p ON p.id = m.purchase_id
     WHERE m.product_id = pc.product_id AND m.quantity_milli > 0 AND p.status = 'Confirmed'),
    pc.recalculated_at
FROM product_costs pc;

DROP TABLE product_costs;
ALTER TABLE product_costs_new RENAME TO product_costs;

-- Typical shelf life in days (loaded as days, weeks or months). Only perishables have one (the service checks it).
ALTER TABLE products ADD COLUMN shelf_life_days INTEGER
    CHECK (shelf_life_days IS NULL OR shelf_life_days BETWEEN 1 AND 3650);
