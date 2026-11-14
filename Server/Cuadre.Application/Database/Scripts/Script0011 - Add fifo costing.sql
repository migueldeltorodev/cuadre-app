-- D-60: what leaves the shelf is costed FIFO, batch by batch (each purchase line is a batch).
-- The batches are not stored: they are rebuilt by replaying the stock ledger, like the average.

-- The batch being sold now (the oldest with stock; with an empty shelf, the newest one). Null until the cache is
-- rebuilt: the seeder replays every product whose current cost is still missing.
ALTER TABLE product_costs ADD COLUMN current_cost_usd_micros INTEGER CHECK (current_cost_usd_micros > 0);
ALTER TABLE product_costs ADD COLUMN current_cost_cup_micros INTEGER CHECK (current_cost_cup_micros > 0);
ALTER TABLE product_costs ADD COLUMN current_purchase_line_id TEXT REFERENCES purchase_lines (id);

-- The colchón part of the line's A2 (cost_cents = real FIFO cost + this). Closings made before D-60 have none.
ALTER TABLE daily_closing_lines ADD COLUMN buffer_cents INTEGER NOT NULL DEFAULT 0;
