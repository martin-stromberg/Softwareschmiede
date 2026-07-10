#!/bin/bash

STEP="$1"
USAGE="$2"

echo "Step: $STEP" >> usage.log
echo "$USAGE" >> usage.log
echo "---" >> usage.log
