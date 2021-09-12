set -x

# python3 MineUsages.py AutoMapper -o 7.0.0 -n 8.0.0 -t old
# python3 MineUsages.py AutoMapper -o 7.0.0 -n 8.0.0 -t new

# python3 MineUsages.py FluentValidation -o 7.0 -n 8.0.0 -t new

# python3 MineUsages.py FluentValidation -o 6.1 -n 7.0 -t new
# python3 MineUsages.py FluentValidation -o 6.1 -n 7.0 -t old

python3 MineUsages.py MediatR -o 5.0.1 -n 6.0.0 -t new
