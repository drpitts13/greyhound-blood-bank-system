---
slug: specimen-label
title: Specimen labels
order: 100
chapter: Catalog
summary: Design the labels printed for specimen tubes and aliquots. Place the barcode and patient and specimen data on a canvas of the label's size. One default design per purpose.
routes:
  - /admin/specimen-labels
  - /admin/specimen-label
---

![Specimen labels](figures/admin-specimen-label.png)

**Specimen Labels** holds the label designs printed for specimens: the label on the collected tube, and labels for aliquots split from it. You can keep several designs. Each has a code, a name, what it is for (tube or aliquot), and its own label size. One active design for tubes and one for aliquots are the defaults; the others are chosen at print time.

## Create a design

Under **New design**, enter a name and a short code (letters, digits, `-` or `_`; the code is what the print desk sees and what each print job records). Choose **Tube** or **Aliquot**, and a label size: 2 × 1, 2.25 × 1.25, 3 × 1, 2 × 0.75, 1.5 × 0.5 (aliquot or cryovial), 1 × 0.5 inches, or **Custom** (0.5 to 4 inches wide, 0.25 to 3 inches tall). Start from a layout for that size, or from a copy of another design. **Create and design** opens it.

## Lay out the label

The middle of the page is the label at scale, with sample data at the size it will print. The left column lists the elements you can place: specimen ID, patient name, patient ID, date of birth, sex, specimen type, collection time, collected by, draw location, static text, and on an aliquot design the aliquot number ("Aliquot 2 of 3").

- Drag an element onto the label, or click it to add it at the top.
- Drag a placed element to move it, or select it and use the arrow keys (one dot at a time; hold Shift for ten). Delete removes it.
- On the right, set the selected element's exact X and Y (in dots at 203 per inch), its font size, and bold.
- The specimen ID can be a barcode (Code 128). Set its bar height and **Bar width** (1 to 4 dots; narrower fits a small label, wider scans more easily), and whether its digits print under it. The canvas draws the barcode as wide as a 12-character specimen ID prints, so you can see whether it fits.

The design is checked as you work:

- The specimen ID must be on the label as a barcode, so the tube can be scanned. Without it **Save** stays unavailable.
- Fewer than two patient identifiers (name, patient ID, date of birth) is a warning. Check it against your laboratory's labeling policy (OCD-080).
- An aliquot design without the aliquot number warns, because aliquots of one specimen would look alike.
- A barcode that may run off the edge or the bottom of the label warns.

**Save** records a new version. Each save, default change, activation, and deactivation is audited with the before and after, and the design's history is listed below the canvas.

## Defaults and printing

In the list, **Make default** makes a design the default for its purpose; the previous default stops being the default. The default cannot be deactivated until another design takes its place. Inactive designs are not offered for printing.

On [Printing](printing), the specimen label card offers the active designs, the default tube design first. Choose an aliquot design and enter how many aliquots: one label prints for each ("Aliquot 1 of 3", "Aliquot 2 of 3", ...), each its own audited print job. An aliquot label's barcode is the parent specimen ID, so scanning an aliquot finds its specimen. A reprint uses the design the label was printed with.

## Test print

Under the canvas, **Test print** prints the layout as it stands, saved or not, so you can try a change before saving it. Choose where it goes: a PDF shown on the page, the ZPL shown on the page, or one of the active Zebra or PDF printers from [Printers](printers). A Zebra printer gets the label scaled to its dots per inch.

The test label carries sample data that cannot pass for a patient: the name reads "TEST LABEL, NOT FOR USE", the patient ID "TEST-ONLY", and the barcode is TESTLABEL01, which matches no specimen. An aliquot design prints "Aliquot 1 of 2". No print job is created; a test label sent to a printer is recorded in the audit trail against the design, with the printer and the design's version. Test print is unavailable while the design has an error.

Print a test label on the printer you will use before relying on a new design, and scan it. A layout that fits on screen can still be hard to scan on a worn printhead or a small label.
