SET NAMES utf8mb4;
USE `cleanhome_order`;
CREATE TABLE IF NOT EXISTS booking_payment_state (
 booking_id BIGINT PRIMARY KEY,
 method VARCHAR(10) NOT NULL,
 status VARCHAR(32) NOT NULL,
 revision BIGINT NOT NULL DEFAULT 0,
 enrolled BOOLEAN NOT NULL DEFAULT FALSE,
 paid_at DATETIME(6) NULL,
 updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(booking_id) REFERENCES bookings(id),
 CHECK(method IN ('CASH','VNPAY'))
) ENGINE=InnoDB;
