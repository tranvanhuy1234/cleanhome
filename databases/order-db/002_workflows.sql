USE `cleanhome_order`;
CREATE TABLE IF NOT EXISTS `workflow_operations` (
  `id` CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `booking_id` BIGINT NOT NULL,
  `actor_id` BIGINT NOT NULL,
  `idempotency_key` VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `kind` VARCHAR(20) NOT NULL,
  `payload` JSON NOT NULL,
  `state` ENUM('pending','succeeded','failed') NOT NULL DEFAULT 'pending',
  `result` JSON NULL,
  `error_status` INT NULL,
  `error_message` VARCHAR(1000) NULL,
  `attempts` INT NOT NULL DEFAULT 0,
  `next_attempt_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `active_booking_id` BIGINT GENERATED ALWAYS AS (CASE WHEN `state`='pending' THEN `booking_id` ELSE NULL END) STORED,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_workflow_actor_key` (`actor_id`,`idempotency_key`),
  UNIQUE KEY `uq_workflow_active_booking` (`active_booking_id`),
  KEY `idx_workflow_due` (`state`,`next_attempt_at`),
  CONSTRAINT `fk_workflow_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `integration_events` (
  `id` CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `event_type` VARCHAR(50) NOT NULL,
  `booking_id` BIGINT NOT NULL,
  `payload` JSON NOT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `dispatched_at` DATETIME(6) NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_booking_event` (`booking_id`,`event_type`),
  CONSTRAINT `fk_event_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB;
