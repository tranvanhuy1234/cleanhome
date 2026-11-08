USE `cleanhome_order`;
CREATE TABLE IF NOT EXISTS `booking_creation_keys` (
 `customer_id` BIGINT NOT NULL,
 `idempotency_key` VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 `request_hash` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 `booking_id` BIGINT NULL,
 PRIMARY KEY (`customer_id`,`idempotency_key`),
 CONSTRAINT `fk_creation_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings`(`id`)
) ENGINE=InnoDB;
