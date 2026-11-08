-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_order` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_order`;

CREATE TABLE `subscriptions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT NOT NULL,
  `package_id` BIGINT NOT NULL,
  `starts_at` DATE NOT NULL,
  `expires_at` DATE NOT NULL,
  `purchase_price` DECIMAL(12,2) NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  `package_name_snapshot` VARCHAR(150) NOT NULL,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  CONSTRAINT `ck_subscription_dates` CHECK (`expires_at` > `starts_at`),
  CONSTRAINT `ck_subscription_price` CHECK (`purchase_price` > 0),
  PRIMARY KEY (`id`),
  KEY `idx_subscriptions_user_id` (`user_id`),
  KEY `idx_subscriptions_package_id` (`package_id`)
) ENGINE=InnoDB COMMENT='Gói khách hàng đã mua';

CREATE TABLE `entitlements` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `subscription_id` BIGINT NOT NULL,
  `service_id` BIGINT NOT NULL,
  `purchased_quantity` DECIMAL(10,2) NOT NULL,
  `unit_snapshot` VARCHAR(20) NOT NULL,
  CONSTRAINT `ck_entitlements_quantity` CHECK (`purchased_quantity` > 0),
  UNIQUE KEY `uq_entitlements_subscription_service` (`subscription_id`, `service_id`),
  PRIMARY KEY (`id`),
  KEY `idx_entitlements_subscription_id` (`subscription_id`),
  KEY `idx_entitlements_service_id` (`service_id`)
) ENGINE=InnoDB COMMENT='Quyền sử dụng đã chốt';

CREATE TABLE `bookings` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `customer_id` BIGINT NOT NULL,
  `branch_id` BIGINT NOT NULL,
  `address_snapshot` JSON NOT NULL,
  `scheduled_start` DATETIME(6) NOT NULL,
  `scheduled_end` DATETIME(6) NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  `total_amount` DECIMAL(12,2) NOT NULL,
  `code` VARCHAR(40) NOT NULL,
  `special_request` TEXT NULL,
  `subtotal_amount` DECIMAL(12,2) NOT NULL,
  `discount_amount` DECIMAL(12,2) NOT NULL DEFAULT 0,
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `version` BIGINT NOT NULL DEFAULT 1,
  UNIQUE KEY `uq_bookings_code` (`code`),
  KEY `idx_bookings_status_start` (`status`, `scheduled_start`),
  CONSTRAINT `ck_bookings_dates` CHECK (`scheduled_end` > `scheduled_start`),
  CONSTRAINT `ck_bookings_amounts` CHECK (`subtotal_amount` >= 0 AND `discount_amount` >= 0 AND `discount_amount` <= `subtotal_amount` AND `total_amount` = `subtotal_amount` - `discount_amount`),
  PRIMARY KEY (`id`),
  KEY `idx_bookings_customer_id` (`customer_id`),
  KEY `idx_bookings_branch_id` (`branch_id`)
) ENGINE=InnoDB COMMENT='Đơn đặt dịch vụ';

CREATE TABLE `booking_items` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NOT NULL,
  `service_id` BIGINT NOT NULL,
  `entitlement_id` BIGINT NULL,
  `service_name_snapshot` VARCHAR(150) NOT NULL,
  `quantity` DECIMAL(10,2) NOT NULL,
  `unit_price` DECIMAL(12,2) NOT NULL,
  `unit_snapshot` VARCHAR(20) NOT NULL,
  CONSTRAINT `ck_booking_items_amounts` CHECK (`quantity` > 0 AND `unit_price` >= 0),
  PRIMARY KEY (`id`),
  KEY `idx_booking_items_booking_id` (`booking_id`),
  KEY `idx_booking_items_service_id` (`service_id`),
  KEY `idx_booking_items_entitlement_id` (`entitlement_id`)
) ENGINE=InnoDB COMMENT='Chi tiết đơn';

CREATE TABLE `booking_status_history` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NOT NULL,
  `changed_by` BIGINT NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  `note` TEXT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_booking_status_history_booking_id` (`booking_id`),
  KEY `idx_booking_status_history_changed_by` (`changed_by`)
) ENGINE=InnoDB COMMENT='Lịch sử tiến độ';

CREATE TABLE `cancellation_requests` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NOT NULL,
  `requested_by` BIGINT NOT NULL,
  `reason` VARCHAR(2000) NOT NULL,
  `status` ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  `requested_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `resolved_by` BIGINT NULL,
  `resolved_at` DATETIME(6) NULL,
  `admin_note` VARCHAR(2000) NULL,
  `pending_booking_id` BIGINT GENERATED ALWAYS AS (CASE WHEN `status` = 'pending' THEN `booking_id` ELSE NULL END) STORED,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_cancel_pending_booking` (`pending_booking_id`),
  KEY `idx_cancellation_booking` (`booking_id`),
  CONSTRAINT `fk_cancellation_booking` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE `entitlement_usages` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `entitlement_id` BIGINT NOT NULL,
  `booking_item_id` BIGINT NOT NULL,
  `quantity` DECIMAL(10,2) NOT NULL,
  `status` ENUM('reserved','used','released') NOT NULL DEFAULT 'reserved',
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_entitlement_usage_item` (`booking_item_id`),
  KEY `idx_entitlement_usage_status` (`entitlement_id`, `status`),
  CONSTRAINT `fk_entitlement_usage_entitlement` FOREIGN KEY (`entitlement_id`) REFERENCES `entitlements` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `fk_entitlement_usage_item` FOREIGN KEY (`booking_item_id`) REFERENCES `booking_items` (`id`) ON DELETE RESTRICT,
  CONSTRAINT `ck_entitlement_usage_quantity` CHECK (`quantity` > 0)
) ENGINE=InnoDB;

CREATE TABLE `voucher_redemptions` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `voucher_id` BIGINT NOT NULL,
  `booking_id` BIGINT NOT NULL,
  `code_snapshot` VARCHAR(50) NOT NULL,
  `eligible_amount` DECIMAL(12,2) NOT NULL,
  `discount_amount` DECIMAL(12,2) NOT NULL,
  `status` ENUM('RESERVED','USED','CANCELLED') NOT NULL DEFAULT 'RESERVED',
  `reserved_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `used_at` DATETIME NULL,
  `cancelled_at` DATETIME NULL,
  `customer_id` BIGINT NOT NULL,
  KEY `idx_redemption_customer_voucher_status` (`customer_id`, `voucher_id`, `status`),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_voucher_redemptions_booking_id` (`booking_id`),
  KEY `idx_voucher_redemptions_voucher_id` (`voucher_id`),
  CONSTRAINT `ck_redemption_amounts` CHECK (`eligible_amount` >= 0 AND `discount_amount` >= 0 AND `discount_amount` <= `eligible_amount`),
  CONSTRAINT `fk_voucher_redemptions_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB COMMENT='Ma ap dung va so tien giam da chot tren moi don';

ALTER TABLE `entitlements` ADD CONSTRAINT `fk_entitlements_subscription_id` FOREIGN KEY (`subscription_id`) REFERENCES `subscriptions` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `booking_items` ADD CONSTRAINT `fk_booking_items_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `booking_items` ADD CONSTRAINT `fk_booking_items_entitlement_id` FOREIGN KEY (`entitlement_id`) REFERENCES `entitlements` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `booking_status_history` ADD CONSTRAINT `fk_booking_status_history_booking_id` FOREIGN KEY (`booking_id`) REFERENCES `bookings` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
