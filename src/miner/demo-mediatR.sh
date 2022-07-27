#!/bin/bash

pushd ../src/miner
echo "======== Step 1: Mining client programs for MediatR from GitHub! ========"
python3 crawler.py MediatR jbogard MediatR -m 400 #mining at most 400 clients

echo "======== Step 2: Mining human adaptations, old usages and new usages from client code repos"
python3 MineEdit.py -f template.json jbogard MediatR --only-mine-library
python3 MineUsages.py -f configuration --only-old
python3 MineUsages.py -f configuration --only-new

popd
