#!/usr/bin/env python2
import csv
import sys

skills_id = set()
status = 0
rowi = 0

f = open('PSO2ACT/skills.csv','r')
reader = csv.reader(f, delimiter=',')
for row in reader:
    name = row[0]
    ID = None
    Type = None
    Detail = None
    if len(row) > 1:
        ID = row[1]
    if len(row) > 2:
        Type = row[2]
    if len(row) > 3:
        Detail = row[3]
    rowi = rowi + 1
    if len(row) != 4:
        print("row %d, wrong column size: %s" % (rowi, row) )
        status = status + 1
    elif rowi != 1 and ID == None:
        print("row %d, Invaild ID: %s" % (rowi, row) )
        status = status + 1
    if ID in skills_id:
        print("row %d, Dup ID: %s" % (rowi, row) )
        status = status + 1
    if rowi != 1 and ID and  not ID.isdigit():
        print("row %d, No ID: %s" % (rowi, row) )
        status = status + 1
    if ID != None:
        skills_id.add(ID)

if status != 0:
   sys.exit("Found Errors in skills.csv")

