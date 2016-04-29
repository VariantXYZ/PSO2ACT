#!/usr/bin/env python2
import csv
import sys

skills_id = set()
status = 0
rowi = 0

f = open('PSO2ACT/skills.csv','r')
reader = csv.reader(f, delimiter=',')
for row in reader:
    rowi = rowi + 1
    if row[1] in skills_id:
        print("Issue at row %d, Dup ID: %s" % (rowi, row) )
        status = status + 1
    if rowi != 1 and not row[1].isdigit():
        print("Issue at row %d, No ID: %s" % (rowi, row) )
        status = status + 1
    skills_id.add(row[1])

if status != 0:
   sys.exit("Found Errors in skills.csv")

