' String Manipulation & Palindrome Inspector
' Uses LEN, MID$, UCASE$, and string concatenation

DIM text AS STRING
DIM reversed AS STRING
DIM n AS INTEGER
DIM i AS INTEGER
DIM ch AS STRING

text = "RADAR"
n = LEN(text)
reversed = ""

PRINT "Inspecting string: "; text

FOR i = n TO 1 STEP -1
    ch = MID$(text, i, 1)
    reversed = reversed + ch
NEXT i

PRINT "Reversed: "; reversed

IF text = reversed THEN
    PRINT "Result: The text IS a palindrome."
ELSE
    PRINT "Result: The text is NOT a palindrome."
END IF

END
