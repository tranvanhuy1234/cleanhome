USE `cleanhome_staff`;
CREATE TABLE IF NOT EXISTS assignment_receipts (
 assignment_id BIGINT NOT NULL,
 accepted_at DATETIME(6) NULL,
 PRIMARY KEY(assignment_id),
 CONSTRAINT fk_receipt_assignment FOREIGN KEY(assignment_id) REFERENCES assignments(id) ON DELETE RESTRICT
) ENGINE=InnoDB;
-- Historical active/done states prove acceptance, but do not reveal its timestamp.
INSERT INTO assignment_receipts(assignment_id,accepted_at)
SELECT a.id,NULL FROM assignments a
WHERE a.status IN ('accepted','in_progress','done')
AND NOT EXISTS(SELECT 1 FROM assignment_receipts r WHERE r.assignment_id=a.id);
