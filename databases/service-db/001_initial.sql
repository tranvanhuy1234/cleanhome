-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_service` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_service`;

CREATE TABLE `service_categories` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(100) NOT NULL,
  `description` TEXT NOT NULL,
  `is_active` BOOLEAN NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB COMMENT='Danh mục dịch vụ';

CREATE TABLE `services` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `category_id` BIGINT NOT NULL,
  `name` VARCHAR(150) NOT NULL,
  `description` TEXT NOT NULL,
  `unit` VARCHAR(20) NOT NULL,
  `base_price` DECIMAL(12,2) NOT NULL,
  `duration_minutes` INT NOT NULL,
  `is_active` BOOLEAN NOT NULL,
  CONSTRAINT `ck_services_price` CHECK (`base_price` >= 0),
  CONSTRAINT `ck_services_duration` CHECK (`duration_minutes` > 0),
  PRIMARY KEY (`id`),
  KEY `idx_services_category_id` (`category_id`)
) ENGINE=InnoDB COMMENT='Dịch vụ';

CREATE TABLE `service_packages` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(150) NOT NULL,
  `price` DECIMAL(12,2) NOT NULL,
  `validity_days` INT NOT NULL,
  `is_active` BOOLEAN NOT NULL,
  CONSTRAINT `ck_packages_price` CHECK (`price` > 0),
  CONSTRAINT `ck_packages_validity` CHECK (`validity_days` > 0),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB COMMENT='Gói nhiều lần / gói năm';

CREATE TABLE `package_items` (
  `package_id` BIGINT NOT NULL,
  `service_id` BIGINT NOT NULL,
  `included_quantity` DECIMAL(10,2) NOT NULL,
  CONSTRAINT `ck_package_items_quantity` CHECK (`included_quantity` > 0),
  PRIMARY KEY (`package_id`, `service_id`),
  KEY `idx_package_items_service_id` (`service_id`)
) ENGINE=InnoDB COMMENT='Dịch vụ trong gói';

CREATE TABLE `vouchers` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `code` VARCHAR(50) NOT NULL,
  `name` VARCHAR(150) NOT NULL,
  `discount_type` ENUM('PERCENT','FIXED') NOT NULL,
  `discount_value` DECIMAL(12,2) NOT NULL,
  `max_discount_amount` DECIMAL(12,2) NULL,
  `min_eligible_amount` DECIMAL(12,2) NOT NULL DEFAULT 0,
  `service_scope` ENUM('ALL','SELECTED') NOT NULL DEFAULT 'ALL',
  `starts_at` DATETIME NOT NULL,
  `expires_at` DATETIME NOT NULL,
  `total_usage_limit` INT NULL,
  `per_user_limit` INT NULL,
  `is_active` BOOLEAN NOT NULL DEFAULT TRUE,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_vouchers_code` (`code`),
  CONSTRAINT `ck_voucher_value` CHECK (`discount_value` > 0 AND (`discount_type` <> 'PERCENT' OR `discount_value` <= 100)),
  CONSTRAINT `ck_voucher_cap` CHECK (`max_discount_amount` IS NULL OR `max_discount_amount` > 0),
  CONSTRAINT `ck_voucher_min` CHECK (`min_eligible_amount` >= 0),
  CONSTRAINT `ck_voucher_dates` CHECK (`expires_at` > `starts_at`),
  CONSTRAINT `ck_voucher_total_limit` CHECK (`total_usage_limit` IS NULL OR `total_usage_limit` > 0),
  CONSTRAINT `ck_voucher_user_limit` CHECK (`per_user_limit` IS NULL OR `per_user_limit` > 0)
) ENGINE=InnoDB COMMENT='Phieu giam gia va dieu kien ap dung';

CREATE TABLE `voucher_services` (
  `voucher_id` BIGINT NOT NULL,
  `service_id` BIGINT NOT NULL,
  PRIMARY KEY (`voucher_id`, `service_id`),
  KEY `idx_voucher_services_service_id` (`service_id`),
  CONSTRAINT `fk_voucher_services_voucher_id` FOREIGN KEY (`voucher_id`) REFERENCES `vouchers` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_voucher_services_service_id` FOREIGN KEY (`service_id`) REFERENCES `services` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB COMMENT='Dich vu duoc ap dung khi service_scope = SELECTED';

ALTER TABLE `services` ADD CONSTRAINT `fk_services_category_id` FOREIGN KEY (`category_id`) REFERENCES `service_categories` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `package_items` ADD CONSTRAINT `fk_package_items_package_id` FOREIGN KEY (`package_id`) REFERENCES `service_packages` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `package_items` ADD CONSTRAINT `fk_package_items_service_id` FOREIGN KEY (`service_id`) REFERENCES `services` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
