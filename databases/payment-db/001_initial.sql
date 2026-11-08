-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_payment` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_payment`;

CREATE TABLE `payments` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NULL,
  `subscription_id` BIGINT NULL,
  `amount` DECIMAL(12,2) NOT NULL,
  `method` VARCHAR(20) NOT NULL,
  `provider_ref` VARCHAR(100) NULL,
  `status` VARCHAR(20) NOT NULL,
  `paid_at` DATETIME(6) NULL,
  `merchant_ref` VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `idempotency_key` VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `customer_id` BIGINT NOT NULL,
  `currency` CHAR(3) NOT NULL DEFAULT 'VND',
  `created_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `version` BIGINT NOT NULL DEFAULT 1,
  UNIQUE KEY `uq_payments_merchant_ref` (`merchant_ref`),
  UNIQUE KEY `uq_payments_idempotency` (`customer_id`, `idempotency_key`),
  CONSTRAINT `ck_payment_target` CHECK ((`booking_id` IS NOT NULL AND `subscription_id` IS NULL) OR (`booking_id` IS NULL AND `subscription_id` IS NOT NULL)),
  CONSTRAINT `ck_payment_amount` CHECK (`amount` > 0),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_payments_provider_ref` (`provider_ref`),
  KEY `idx_payments_booking_id` (`booking_id`),
  KEY `idx_payments_subscription_id` (`subscription_id`)
) ENGINE=InnoDB COMMENT='Thanh toán';

CREATE TABLE `revenue_shares` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `payment_id` BIGINT NOT NULL,
  `branch_id` BIGINT NULL,
  `employee_id` BIGINT NULL,
  `beneficiary_type` VARCHAR(20) NOT NULL,
  `amount` DECIMAL(12,2) NOT NULL,
  `settlement_status` VARCHAR(20) NOT NULL,
  CONSTRAINT `ck_revenue_share_amount` CHECK (`amount` >= 0),
  PRIMARY KEY (`id`),
  KEY `idx_revenue_shares_payment_id` (`payment_id`),
  KEY `idx_revenue_shares_branch_id` (`branch_id`),
  KEY `idx_revenue_shares_employee_id` (`employee_id`)
) ENGINE=InnoDB COMMENT='Phân chia doanh thu';

ALTER TABLE `revenue_shares` ADD CONSTRAINT `fk_revenue_shares_payment_id` FOREIGN KEY (`payment_id`) REFERENCES `payments` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
