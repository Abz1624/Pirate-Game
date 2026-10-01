# V5 M3c: fitting the XP table to the level-up gaps

Note: this fit ran before the interim enemy scale (× 0.85), the evolution level 5 and Roxie's hull 104; `level-times.md`
checks the table on the final data (cumulative times within about ±10%). The committed table keeps the 14 measured
levels (rounds' medians are NaN past them).

Target: level 1 with the first kill, then gaps of 10, 10, 20, 30, 50 s (Fibonacci × 10 s), then 60 s. Fitted from the old curve; the baseline personality, 60 unsinkable runs (no deaths, so every level is measured; a flawless ship collects a little faster than a real one), seed 11, per round; stops when the mean time error is under 6%.

## Round 1: mean time error 39.9% (worst single gap 76%)

| Level | XP to next | Target time | Median time | Target gap | Median gap |
|---|---|---|---|---|---|
| 1 | 1 | 12 | 13 | 12 | 13 |
| 2 | 4 | 22 | 23 | 10 | 10 |
| 3 | 6 | 32 | 35 | 10 | 12 |
| 4 | 8 | 52 | 49 | 20 | 14 |
| 5 | 10 | 82 | 65 | 30 | 16 |
| 6 | 12 | 132 | 81 | 50 | 16 |
| 7 | 15 | 192 | 95 | 60 | 14 |
| 8 | 18 | 252 | 112 | 60 | 17 |
| 9 | 21 | 312 | 129 | 60 | 17 |
| 10 | 24 | 372 | 145 | 60 | 17 |
| 11 | 28 | 432 | 181 | 60 | 35 |
| 12 | 31 | 492 | 206 | 60 | 25 |
| 13 | 35 | 552 | 231 | 60 | 25 |
| 14 | 39 | 612 | 257 | 60 | 26 |
| 15 | 43 | 672 | 278 | 60 | 21 |
| 16 | 47 | 732 | 300 | 60 | 22 |
| 17 | 51 | 792 | 326 | 60 | 26 |
| 18 | 55 | 852 | 351 | 60 | 25 |

## Round 2: mean time error 19.0% (worst single gap 65%)

| Level | XP to next | Target time | Median time | Target gap | Median gap |
|---|---|---|---|---|---|
| 1 | 1 | 12 | 13 | 12 | 13 |
| 2 | 4 | 22 | 23 | 10 | 10 |
| 3 | 6 | 32 | 35 | 10 | 12 |
| 4 | 10 | 52 | 52 | 20 | 17 |
| 5 | 14 | 82 | 74 | 30 | 22 |
| 6 | 24 | 132 | 103 | 50 | 29 |
| 7 | 34 | 192 | 138 | 60 | 35 |
| 8 | 39 | 252 | 192 | 60 | 54 |
| 9 | 46 | 312 | 241 | 60 | 49 |
| 10 | 53 | 372 | 274 | 60 | 33 |
| 11 | 53 | 432 | 307 | 60 | 33 |
| 12 | 57 | 492 | 344 | 60 | 36 |
| 13 | 65 | 552 | 433 | 60 | 89 |
| 14 | 70 | 612 | 454 | 60 | 21 |
| 15 | 85 | 672 | 492 | 60 | 38 |
| 16 | 91 | 732 | 543 | 60 | 51 |
| 17 | 93 | 792 | 571 | 60 | 28 |
| 18 | 102 | 852 | 620 | 60 | 48 |

## Round 3: mean time error 7.0% (worst single gap 76%)

| Level | XP to next | Target time | Median time | Target gap | Median gap |
|---|---|---|---|---|---|
| 1 | 1 | 12 | 13 | 12 | 13 |
| 2 | 4 | 22 | 23 | 10 | 10 |
| 3 | 6 | 32 | 35 | 10 | 12 |
| 4 | 11 | 52 | 54 | 20 | 19 |
| 5 | 17 | 82 | 78 | 30 | 24 |
| 6 | 33 | 132 | 116 | 50 | 38 |
| 7 | 47 | 192 | 164 | 60 | 48 |
| 8 | 47 | 252 | 229 | 60 | 64 |
| 9 | 54 | 312 | 271 | 60 | 43 |
| 10 | 73 | 372 | 329 | 60 | 58 |
| 11 | 74 | 432 | 416 | 60 | 88 |
| 12 | 78 | 492 | 489 | 60 | 73 |
| 13 | 78 | 552 | 546 | 60 | 57 |
| 14 | 105 | 612 | 581 | 60 | 34 |
| 15 | 111 | 672 | 595 | 60 | 14 |
| 16 | 111 | 732 | – | 60 | – |
| 17 | 133 | 792 | – | 60 | – |
| 18 | 133 | 852 | – | 60 | – |

## Round 4: mean time error 4.9% (worst single gap 183%)

| Level | XP to next | Target time | Median time | Target gap | Median gap |
|---|---|---|---|---|---|
| 1 | 1 | 12 | 13 | 12 | 13 |
| 2 | 4 | 22 | 23 | 10 | 10 |
| 3 | 6 | 32 | 35 | 10 | 12 |
| 4 | 11 | 52 | 54 | 20 | 19 |
| 5 | 19 | 82 | 80 | 30 | 26 |
| 6 | 38 | 132 | 126 | 50 | 46 |
| 7 | 54 | 192 | 195 | 60 | 70 |
| 8 | 54 | 252 | 256 | 60 | 61 |
| 9 | 65 | 312 | 307 | 60 | 51 |
| 10 | 77 | 372 | 372 | 60 | 65 |
| 11 | 77 | 432 | 481 | 60 | 109 |
| 12 | 77 | 492 | 547 | 60 | 67 |
| 13 | 80 | 552 | 625 | 60 | 78 |
| 14 | 125 | 612 | 575 | 60 | -50 |
| 15 | 153 | 672 | – | 60 | – |
| 16 | 153 | 732 | – | 60 | – |
| 17 | 153 | 792 | – | 60 | – |
| 18 | 153 | 852 | – | 60 | – |

Chosen table (the round with the smallest mean time error): 1, 4, 6, 11, 19, 38, 54, 54, 65, 77, 77, 77, 80, 125, 153, 153, 153, 153
