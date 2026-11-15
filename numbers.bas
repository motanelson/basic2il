' Prime Number Finder in Label-Based BASIC
' Uses loops, conditionals, integer division, and output

DIM maxLimit AS INTEGER
DIM candidate AS INTEGER
DIM divisor AS INTEGER
DIM isPrime AS INTEGER

PRINT "=== Prime Numbers Generator ==="
maxLimit = 30
PRINT "Searching primes up to "; maxLimit

FOR candidate = 2 TO maxLimit
    isPrime = 1
    FOR divisor = 2 TO candidate - 1
        IF (candidate MOD divisor) = 0 THEN isPrime = 0
        IF (candidate MOD divisor) = 0 THEN GOTO EXIT_INNER
            
            
        
    NEXT divisor
EXIT_INNER:
    IF isPrime = 1 THEN  PRINT candidate; " is PRIME"
    END IF
NEXT candidate

PRINT "Prime search complete."
END

