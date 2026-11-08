SET NAMES utf8mb4;
USE `cleanhome_order`;
CREATE TABLE IF NOT EXISTS booking_archives(
 booking_id BIGINT PRIMARY KEY,deleted_at DATETIME(6) NULL,deleted_by BIGINT NULL,delete_reason VARCHAR(2000) NULL,
 FOREIGN KEY(booking_id) REFERENCES bookings(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS booking_dispatch_notices(
 booking_id BIGINT PRIMARY KEY,last_hash VARCHAR(100) NULL,checked_at DATETIME(6) NULL,
 FOREIGN KEY(booking_id) REFERENCES bookings(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS order_management_audit(
 id BIGINT AUTO_INCREMENT PRIMARY KEY,booking_id BIGINT NOT NULL,actor_id BIGINT NOT NULL,
 action VARCHAR(30) NOT NULL,reason VARCHAR(2000) NOT NULL,created_at DATETIME(6) NOT NULL,
 FOREIGN KEY(booking_id) REFERENCES bookings(id)
) ENGINE=InnoDB;
