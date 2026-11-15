' Factorial & Subroutine Routine using Labels
' Uses GOSUB, RETURN, GOTO, arithmetic, and console I/O

DIM num AS INTEGER
DIM result AS INTEGER
DIM counter AS INTEGER

PRINT "--- Factorial Calculation ---"
num = 6
result = 1
counter = 1

GOSUB COMPUTE_FACT

PRINT "Factorial of "; num; " is "; result
GOTO DONE

COMPUTE_FACT:
FACT_LOOP:
    IF counter > num THEN RETURN
    
    result = result * counter
    counter = counter + 1
    GOTO FACT_LOOP

DONE:
PRINT "Execution finished normally."
END
