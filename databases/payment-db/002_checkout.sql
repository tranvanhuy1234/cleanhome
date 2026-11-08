SET NAMES utf8mb4;
USE `cleanhome_payment`;
CREATE TABLE IF NOT EXISTS payment_orders (
 booking_id BIGINT PRIMARY KEY, customer_id BIGINT NOT NULL, code VARCHAR(40) NOT NULL,
 amount DECIMAL(12,2) NOT NULL, method VARCHAR(10) NOT NULL,
 status VARCHAR(32) NOT NULL, revision BIGINT NOT NULL DEFAULT 1,
 collector_employee_id BIGINT NULL, collector_user_id BIGINT NULL,
 collected_at DATETIME(6) NULL, paid_at DATETIME(6) NULL,
 synced_revision BIGINT NOT NULL DEFAULT 0,
 last_checked_at DATETIME(6) NULL,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CHECK(amount>0), CHECK(method IN ('CASH','VNPAY'))
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS payment_attempts (
 payment_id BIGINT PRIMARY KEY,
 create_date CHAR(14) NOT NULL, expires_at DATETIME(6) NOT NULL,
 payment_url TEXT NOT NULL, response_code VARCHAR(10) NULL,
 callback_hash CHAR(64) NULL,
 FOREIGN KEY(payment_id) REFERENCES payments(id)
) ENGINE=InnoDB;
ALTER TABLE payment_attempts ADD COLUMN IF NOT EXISTS last_query_at DATETIME(6) NULL;
CREATE TABLE IF NOT EXISTS payment_events (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, booking_id BIGINT NOT NULL,
 payment_id BIGINT NULL, actor_id BIGINT NULL, event_type VARCHAR(50) NOT NULL,
 note VARCHAR(2000) NOT NULL, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(booking_id) REFERENCES payment_orders(booking_id),
 INDEX(booking_id,id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS payment_refund_cases (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, booking_id BIGINT NOT NULL,
 payment_id BIGINT NULL, amount DECIMAL(12,2) NOT NULL,
 status VARCHAR(30) NOT NULL DEFAULT 'pending', evidence VARCHAR(2000) NULL,
 resolved_by BIGINT NULL, resolved_at DATETIME(6) NULL,
 UNIQUE(booking_id,payment_id), FOREIGN KEY(booking_id) REFERENCES payment_orders(booking_id)
) ENGINE=InnoDB;
