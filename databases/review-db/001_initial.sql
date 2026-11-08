-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_review` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_review`;

CREATE TABLE `reviews` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NOT NULL,
  `rating` SMALLINT NOT NULL,
  `comment` TEXT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  `employee_id` BIGINT NOT NULL,
  `customer_id` BIGINT NOT NULL,
  `assignment_id` BIGINT NOT NULL,
  `is_hidden` BOOLEAN NOT NULL DEFAULT FALSE,
  `hidden_reason` VARCHAR(2000) NULL,
  `moderated_by` BIGINT NULL,
  `moderated_at` DATETIME(6) NULL,
  KEY `idx_reviews_employee_created` (`employee_id`, `created_at`),
  CONSTRAINT `ck_reviews_rating` CHECK (`rating` BETWEEN 1 AND 5),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_reviews_booking_employee` (`booking_id`, `employee_id`)
) ENGINE=InnoDB COMMENT='Đánh giá sau dịch vụ';


