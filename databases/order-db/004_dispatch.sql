USE `cleanhome_order`;
CREATE TABLE IF NOT EXISTS booking_dispatch (
 booking_id BIGINT NOT NULL PRIMARY KEY,
 required_staff INT NOT NULL,
 payment_state VARCHAR(20) NOT NULL DEFAULT 'unpaid',
 area_label VARCHAR(150) NOT NULL DEFAULT 'Khu vực chi nhánh',
 state VARCHAR(30) NOT NULL DEFAULT 'queued',
 last_error VARCHAR(500) NULL,
 updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CONSTRAINT fk_dispatch_booking FOREIGN KEY(booking_id) REFERENCES bookings(id),
 CONSTRAINT ck_dispatch_required CHECK(required_staff BETWEEN 1 AND 20)
) ENGINE=InnoDB;
