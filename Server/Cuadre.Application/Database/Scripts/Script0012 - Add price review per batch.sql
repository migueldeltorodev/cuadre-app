-- D-61: the suggested price follows the batch being sold (D-60). The app never changes a price by itself: it
-- tells when a new batch went on sale, and when a batch waiting behind will not fit in today's price.

-- The newest purchase line (the "last cost" batch), so a notice can name it. Filled by the seeder's replay.
ALTER TABLE product_costs ADD COLUMN last_purchase_line_id TEXT REFERENCES purchase_lines (id);

-- The batch the current price was decided for: set whenever the price changes, or when the user keeps it.
ALTER TABLE products ADD COLUMN price_reviewed_line_id TEXT REFERENCES purchase_lines (id);

-- The newest batch whose "it will not fit in the price" warning the user already dismissed.
ALTER TABLE products ADD COLUMN costlier_seen_line_id TEXT REFERENCES purchase_lines (id);

-- Prices in force today were decided for the batch on sale now: no notice for them on the first start.
UPDATE products
SET price_reviewed_line_id = (SELECT pc.current_purchase_line_id FROM product_costs pc WHERE pc.product_id = products.id);
