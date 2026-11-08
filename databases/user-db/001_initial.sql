-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_user` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_user`;

CREATE TABLE `profiles` (
  `user_id` BIGINT NOT NULL COMMENT 'External auth.accounts.id',
  `full_name` VARCHAR(100) NOT NULL,
  `phone` VARCHAR(20) NOT NULL,
  `avatar_url` TEXT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `uq_profiles_phone` (`phone`)
) ENGINE=InnoDB;

CREATE TABLE `addresses` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT NOT NULL,
  `recipient` VARCHAR(100) NOT NULL,
  `phone` VARCHAR(20) NOT NULL,
  `address_text` TEXT NOT NULL,
  `latitude` DECIMAL(10,7) NOT NULL,
  `longitude` DECIMAL(10,7) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_addresses_user_id` (`user_id`)
) ENGINE=InnoDB COMMENT='Địa chỉ khách hàng';

CREATE TABLE `notifications` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT NOT NULL,
  `booking_id` BIGINT NULL,
  `content` TEXT NOT NULL,
  `read_at` DATETIME(6) NULL,
  `created_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_notifications_user_id` (`user_id`),
  KEY `idx_notifications_booking_id` (`booking_id`)
) ENGINE=InnoDB COMMENT='Thông báo';

ALTER TABLE `addresses` ADD CONSTRAINT `fk_addresses_profile` FOREIGN KEY (`user_id`) REFERENCES `profiles` (`user_id`) ON DELETE RESTRICT;
ALTER TABLE `notifications` ADD CONSTRAINT `fk_notifications_profile` FOREIGN KEY (`user_id`) REFERENCES `profiles` (`user_id`) ON DELETE RESTRICT;
