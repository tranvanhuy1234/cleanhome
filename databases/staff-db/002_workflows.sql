USE `cleanhome_staff`;
CREATE TABLE IF NOT EXISTS `processed_operations` (
  `operation_id` CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `payload_hash` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `result` JSON NOT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`operation_id`)
) ENGINE=InnoDB;
