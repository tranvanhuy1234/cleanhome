SET NAMES utf8mb4;
USE `cleanhome_staff`;
CREATE TABLE IF NOT EXISTS withdrawal_policy(id INT PRIMARY KEY,early_minutes INT NOT NULL DEFAULT 120,CHECK(early_minutes BETWEEN 0 AND 10080)) ENGINE=InnoDB;
INSERT IGNORE INTO withdrawal_policy(id,early_minutes) VALUES(1,120);
CREATE TABLE IF NOT EXISTS assignment_withdrawals(
 id BIGINT AUTO_INCREMENT PRIMARY KEY,assignment_id BIGINT NOT NULL,requested_by BIGINT NOT NULL,
 reason VARCHAR(2000) NOT NULL,state VARCHAR(20) NOT NULL,
 requested_at DATETIME(6) NOT NULL,reviewed_by BIGINT NULL,reviewed_at DATETIME(6) NULL,review_note VARCHAR(2000) NULL,
 pending_assignment BIGINT GENERATED ALWAYS AS(IF(state='pending',assignment_id,NULL)) STORED,
 UNIQUE(pending_assignment),FOREIGN KEY(assignment_id) REFERENCES assignments(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS assignment_exits(
 id BIGINT AUTO_INCREMENT PRIMARY KEY,assignment_id BIGINT NOT NULL,operation_id CHAR(32) NOT NULL,
 actor_id BIGINT NOT NULL,source VARCHAR(30) NOT NULL,reason VARCHAR(2000) NOT NULL,
 created_at DATETIME(6) NOT NULL,UNIQUE(assignment_id,operation_id),
 FOREIGN KEY(assignment_id) REFERENCES assignments(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS replacement_assignments(assignment_id BIGINT PRIMARY KEY,created_at DATETIME(6) NOT NULL,FOREIGN KEY(assignment_id) REFERENCES assignments(id)) ENGINE=InnoDB;
