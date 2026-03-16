#!/usr/bin/env bash
set -euo pipefail

# Run benchmarks on an Azure Spot VM with a colocated premium storage account.
# Usage: ./bench-azure.sh [region]
# Default region: norwayeast

RG="binreader-bench-rg"
VM_NAME="bench-vm"
VM_SIZE="Standard_D2s_v5"
VM_IMAGE="Ubuntu2404"
SPOT_MAX_PRICE="0.03"
STORAGE_ACCOUNT="binreaderbench$RANDOM"
LOCAL_RESULTS="./BenchmarkDotNet.Artifacts-azure"
LOCATION="${1:-norwayeast}"

cleanup() {
  echo "==> Cleaning up resource group $RG..."
  az group delete --name "$RG" --yes --no-wait 2>/dev/null || true
}
trap cleanup EXIT

echo "==> Creating resource group $RG in $LOCATION"
az group create --name "$RG" --location "$LOCATION" -o none

echo "==> Creating premium storage account $STORAGE_ACCOUNT..."
az storage account create \
  --resource-group "$RG" \
  --name "$STORAGE_ACCOUNT" \
  --location "$LOCATION" \
  --sku Premium_LRS \
  --kind BlockBlobStorage \
  --min-tls-version TLS1_2 \
  -o none

CONN_STRING=$(az storage account show-connection-string \
  --resource-group "$RG" \
  --name "$STORAGE_ACCOUNT" \
  --query connectionString -o tsv)

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
$SSH "cd ~/src && export PATH=\$HOME/.dotnet:\$PATH && export AZURE_STORAGE_CONNECTION_STRING='$CONN_STRING' && dotnet run -c Release"

echo "==> Copying results back..."
rm -rf "$LOCAL_RESULTS"
$SCP -r "azureuser@$VM_IP:~/src/BenchmarkDotNet.Artifacts" "$LOCAL_RESULTS"

echo "==> Results saved to $LOCAL_RESULTS"
echo "==> Cleanup will run automatically (trap on EXIT)"
