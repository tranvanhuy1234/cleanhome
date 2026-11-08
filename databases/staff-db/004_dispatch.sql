USE `cleanhome_staff`;
CREATE TABLE IF NOT EXISTS dispatch_settings(id INT PRIMARY KEY,body JSON NOT NULL) ENGINE=InnoDB;
INSERT INTO dispatch_settings(id,body) SELECT 1,'{"enabled":true,"offerSeconds":60,"searchSeconds":180,"positionTtlSeconds":300,"maxAccuracyMeters":500,"firstRadiusKm":5,"secondRadiusKm":10,"groupSize":10,"allowBranchFallback":true}' WHERE NOT EXISTS(SELECT 1 FROM dispatch_settings WHERE id=1);
CREATE TABLE IF NOT EXISTS employee_presence(
 employee_id BIGINT PRIMARY KEY,ready BOOLEAN NOT NULL DEFAULT FALSE,
 latitude DECIMAL(10,7) NULL,longitude DECIMAL(10,7) NULL,accuracy DECIMAL(10,2) NULL,
 position_at DATETIME(6) NULL,ready_since DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CONSTRAINT fk_presence_employee FOREIGN KEY(employee_id) REFERENCES employees(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS dispatch_service_rules(service_id BIGINT PRIMARY KEY,skill_ids JSON NOT NULL) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS dispatch_jobs(
 booking_id BIGINT PRIMARY KEY,payload JSON NOT NULL,required_staff INT NOT NULL,
 state VARCHAR(30) NOT NULL DEFAULT 'searching',round_number INT NOT NULL DEFAULT 0,cycle_round INT NOT NULL DEFAULT 0,
 offer_until DATETIME(6) NULL,search_until DATETIME(6) NOT NULL,
 updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 CONSTRAINT ck_job_slots CHECK(required_staff BETWEEN 1 AND 20)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS dispatch_booking_locks(booking_id BIGINT PRIMARY KEY,stopped BOOLEAN NOT NULL DEFAULT FALSE) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS dispatch_offers(
 id BIGINT PRIMARY KEY AUTO_INCREMENT,booking_id BIGINT NOT NULL,employee_id BIGINT NOT NULL,
 state VARCHAR(20) NOT NULL DEFAULT 'pending',expires_at DATETIME(6) NOT NULL,
 distance_km DECIMAL(10,1) NULL,round_number INT NOT NULL,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),responded_at DATETIME(6) NULL,
 UNIQUE KEY uq_offer_employee_round(booking_id,employee_id,round_number),
 KEY idx_employee_pending(employee_id,state,expires_at),
 CONSTRAINT fk_offer_job FOREIGN KEY(booking_id) REFERENCES dispatch_jobs(booking_id),
 CONSTRAINT fk_offer_employee FOREIGN KEY(employee_id) REFERENCES employees(id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS dispatch_audit(
 id BIGINT PRIMARY KEY AUTO_INCREMENT,booking_id BIGINT NOT NULL,employee_id BIGINT NULL,
 event_type VARCHAR(40) NOT NULL,created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY idx_dispatch_audit(booking_id,id),CONSTRAINT fk_audit_job FOREIGN KEY(booking_id) REFERENCES dispatch_jobs(booking_id)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS assignment_arrivals(assignment_id BIGINT PRIMARY KEY,arrived_at DATETIME(6) NOT NULL,CONSTRAINT fk_arrival_assignment FOREIGN KEY(assignment_id) REFERENCES assignments(id)) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS assignment_support(id BIGINT PRIMARY KEY AUTO_INCREMENT,assignment_id BIGINT NOT NULL,reason VARCHAR(2000) NOT NULL,status VARCHAR(20) NOT NULL DEFAULT 'pending',created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),resolved_at DATETIME(6) NULL,CONSTRAINT fk_support_assignment FOREIGN KEY(assignment_id) REFERENCES assignments(id),KEY idx_support_status(status,id)) ENGINE=InnoDB;
