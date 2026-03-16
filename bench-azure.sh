#!/usr/bin/env bash
set -euo pipefail

# Run benchmarks on an Azure Spot VM in the same region as the storage account.
# Usage: export AZURE_STORAGE_CONNECTION_STRING="..."; ./bench-azure.sh [region]

RG="binreader-bench-rg"
VM_NAME="bench-vm"
VM_SIZE="Standard_D2s_v5"
VM_IMAGE="Ubuntu2404"
SPOT_MAX_PRICE="0.03"
LOCAL_RESULTS="./BenchmarkDotNet.Artifacts-azure"

if [[ -z "${AZURE_STORAGE_CONNECTION_STRING:-}" ]]; then
  echo "ERROR: AZURE_STORAGE_CONNECTION_STRING is not set" >&2
  exit 1
fi

# Region: argument > auto-detect from storage account
if [[ -n "${1:-}" ]]; then
  LOCATION="$1"
else
  # Extract account name from connection string and look up its location
  ACCOUNT_NAME=$(echo "$AZURE_STORAGE_CONNECTION_STRING" | grep -oP 'AccountName=\K[^;]+')
  echo "Auto-detecting region from storage account '$ACCOUNT_NAME'..."
  LOCATION=$(az storage account show --name "$ACCOUNT_NAME" --query location -o tsv)
  echo "Detected region: $LOCATION"
fi

cleanup() {
  echo "Cleaning up resource group $RG..."
  az group delete --name "$RG" --yes --no-wait 2>/dev/null || true
}
trap cleanup EXIT

echo "==> Creating resource group $RG in $LOCATION"
az group create --name "$RG" --location "$LOCATION" -o none

echo "==> Creating spot VM ($VM_SIZE)..."
az vm create \
  --resource-group "$RG" \
  --name "$VM_NAME" \
  --image "$VM_IMAGE" \
  --size "$VM_SIZE" \
  --priority Spot \
  --max-price "$SPOT_MAX_PRICE" \
  --eviction-policy Delete \
  --admin-username azureuser \
  --generate-ssh-keys \
  --public-ip-sku Standard \
  -o none

VM_IP=$(az vm show --resource-group "$RG" --name "$VM_NAME" -d --query publicIps -o tsv)
SSH="ssh -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null azureuser@$VM_IP"
SCP="scp -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null"

echo "==> VM ready at $VM_IP"

echo "==> Installing .NET 10.0 on VM..."
$SSH "curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 && echo 'export PATH=\$HOME/.dotnet:\$PATH' >> ~/.bashrc"

echo "==> Copying source code to VM..."
$SCP -r \
  Benchmarks DataGeneration Models PackedBlobFormat Query Services \
  Program.cs binreader.csproj binreaderpoc.sln \
  "azureuser@$VM_IP:~/src/"

echo "==> Running benchmarks on VM..."
$SSH "cd ~/src && export PATH=\$HOME/.dotnet:\$PATH && export AZURE_STORAGE_CONNECTION_STRING='$AZURE_STORAGE_CONNECTION_STRING' && dotnet run -c Release"

echo "==> Copying results back..."
rm -rf "$LOCAL_RESULTS"
$SCP -r "azureuser@$VM_IP:~/src/BenchmarkDotNet.Artifacts" "$LOCAL_RESULTS"

echo "==> Results saved to $LOCAL_RESULTS"
echo "==> Cleanup will run automatically (trap on EXIT)"
