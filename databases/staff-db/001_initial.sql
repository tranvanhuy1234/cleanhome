-- CleanHome foundation V001 - MariaDB 10.4.32
-- NEW DATABASE ONLY. Run once; no automatic migration or data copy.
-- DATETIME values are UTC; external IDs have no cross-service foreign keys.
SET NAMES utf8mb4;
SET time_zone = '+00:00';
CREATE DATABASE IF NOT EXISTS `cleanhome_staff` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `cleanhome_staff`;

CREATE TABLE `branches` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(150) NOT NULL,
  `address_text` TEXT NOT NULL,
  `service_area` JSON NOT NULL,
  `latitude` DECIMAL(10,7) NOT NULL,
  `longitude` DECIMAL(10,7) NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB COMMENT='Chi nhánh / đối tác';

CREATE TABLE `employees` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `user_id` BIGINT NOT NULL,
  `branch_id` BIGINT NOT NULL,
  `verification_status` VARCHAR(20) NOT NULL,
  `experience_years` INT NOT NULL,
  `response_rate` DECIMAL(5,2) NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_employees_user_id` (`user_id`),
  KEY `idx_employees_branch_id` (`branch_id`)
) ENGINE=InnoDB COMMENT='Hồ sơ nhân viên';

CREATE TABLE `employee_availability` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `employee_id` BIGINT NOT NULL,
  `starts_at` DATETIME(6) NOT NULL,
  `ends_at` DATETIME(6) NOT NULL,
  `status` VARCHAR(20) NOT NULL,
  CONSTRAINT `ck_availability_dates` CHECK (`ends_at` > `starts_at`),
  KEY `idx_availability_employee_dates` (`employee_id`, `starts_at`, `ends_at`),
  PRIMARY KEY (`id`),
  KEY `idx_employee_availability_employee_id` (`employee_id`)
) ENGINE=InnoDB COMMENT='Lịch làm việc';

CREATE TABLE `skills` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `name` VARCHAR(100) NOT NULL,
  `description` TEXT NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB COMMENT='Kỹ năng';

CREATE TABLE `employee_skills` (
  `employee_id` BIGINT NOT NULL,
  `skill_id` BIGINT NOT NULL,
  `level` VARCHAR(20) NOT NULL,
  PRIMARY KEY (`employee_id`, `skill_id`),
  KEY `idx_employee_skills_skill_id` (`skill_id`)
) ENGINE=InnoDB COMMENT='Kỹ năng nhân viên';

CREATE TABLE `certificates` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `employee_id` BIGINT NOT NULL,
  `skill_id` BIGINT NOT NULL,
  `document_url` TEXT NOT NULL,
  `issued_at` DATE NOT NULL,
  `expires_at` DATE NULL,
  `verification_status` VARCHAR(20) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_certificates_employee_id` (`employee_id`),
  KEY `idx_certificates_skill_id` (`skill_id`)
) ENGINE=InnoDB COMMENT='Chứng nhận nhân viên';

CREATE TABLE `assignments` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `booking_id` BIGINT NOT NULL,
  `employee_id` BIGINT NOT NULL,
  `assigned_at` DATETIME(6) NOT NULL,
  `check_in_at` DATETIME(6) NULL,
  `check_out_at` DATETIME(6) NULL,
  `status` VARCHAR(20) NOT NULL,
  `scheduled_start` DATETIME(6) NOT NULL,
  `scheduled_end` DATETIME(6) NOT NULL,
  `assigned_by` BIGINT NOT NULL,
  `version` BIGINT NOT NULL DEFAULT 1,
  UNIQUE KEY `uq_assignment_booking_employee` (`booking_id`, `employee_id`),
  KEY `idx_assignment_employee_dates` (`employee_id`, `scheduled_start`, `scheduled_end`, `status`),
  CONSTRAINT `ck_assignment_dates` CHECK (`scheduled_end` > `scheduled_start`),
  CONSTRAINT `ck_assignment_checkout` CHECK (`check_out_at` IS NULL OR (`check_in_at` IS NOT NULL AND `check_out_at` >= `check_in_at`)),
  PRIMARY KEY (`id`),
  KEY `idx_assignments_booking_id` (`booking_id`),
  KEY `idx_assignments_employee_id` (`employee_id`)
) ENGINE=InnoDB COMMENT='Phân công nhân viên';

ALTER TABLE `employees` ADD CONSTRAINT `fk_employees_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branches` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `employee_availability` ADD CONSTRAINT `fk_employee_availability_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `employee_skills` ADD CONSTRAINT `fk_employee_skills_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `employee_skills` ADD CONSTRAINT `fk_employee_skills_skill_id` FOREIGN KEY (`skill_id`) REFERENCES `skills` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `certificates` ADD CONSTRAINT `fk_certificates_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `certificates` ADD CONSTRAINT `fk_certificates_skill_id` FOREIGN KEY (`skill_id`) REFERENCES `skills` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
ALTER TABLE `assignments` ADD CONSTRAINT `fk_assignments_employee_id` FOREIGN KEY (`employee_id`) REFERENCES `employees` (`id`) ON DELETE RESTRICT ON UPDATE RESTRICT;
