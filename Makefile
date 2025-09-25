# Makefile for DotNetShell project

# Variables
IMAGE_NAME := dotnetshell
IMAGE_TAG := build
DOCKERFILE := Dockerfile.build
CONTAINER_NAME := dotnetshell-container

# Colors for output
GREEN := \033[0;32m
YELLOW := \033[0;33m
RED := \033[0;31m
NC := \033[0m # No Color

.PHONY: help build run clean stop logs shell rebuild

# Default target
help:
	@echo "$(GREEN)Available targets:$(NC)"
	@echo "  $(YELLOW)build$(NC)       - Build the Docker image using Dockerfile.build"
	@echo "  $(YELLOW)run$(NC)         - Run the container from the built image"
	@echo "  $(YELLOW)run-example$(NC) - Run example module (minimal host, no health endpoints)"
	@echo "  $(YELLOW)run-shell$(NC)   - Run with FULL shell (includes /health endpoints)"
	@echo "  $(YELLOW)stop$(NC)        - Stop and remove the running container"
	@echo "  $(YELLOW)clean$(NC)       - Remove the Docker image"
	@echo "  $(YELLOW)logs$(NC)        - Show container logs"
	@echo "  $(YELLOW)shell$(NC)       - Open a shell in the running container"
	@echo "  $(YELLOW)rebuild$(NC)     - Clean and build the image (force rebuild)"

# Build the Docker image
build:
	@echo "$(GREEN)Building Docker image $(IMAGE_NAME):$(IMAGE_TAG)...$(NC)"
	docker build -f $(DOCKERFILE) -t $(IMAGE_NAME):$(IMAGE_TAG) .
	@echo "$(GREEN)Build complete!$(NC)"

# Run the container
run:
	@echo "$(GREEN)Running container $(CONTAINER_NAME)...$(NC)"
	docker run --rm --name $(CONTAINER_NAME) $(IMAGE_NAME):$(IMAGE_TAG)

# Stop and remove the container
stop:
	@echo "$(YELLOW)Stopping container $(CONTAINER_NAME)...$(NC)"
	-docker stop $(CONTAINER_NAME) 2>/dev/null || true
	-docker rm $(CONTAINER_NAME) 2>/dev/null || true
	@echo "$(GREEN)Container stopped and removed.$(NC)"

# Remove the Docker image
clean:
	@echo "$(YELLOW)Removing Docker image $(IMAGE_NAME):$(IMAGE_TAG)...$(NC)"
	-docker rmi $(IMAGE_NAME):$(IMAGE_TAG) 2>/dev/null || true
	@echo "$(GREEN)Image removed.$(NC)"

# Show container logs
logs:
	@echo "$(GREEN)Showing logs for container $(CONTAINER_NAME)...$(NC)"
	docker logs $(CONTAINER_NAME)

# Open a shell in the running container
shell:
	@echo "$(GREEN)Opening shell in container $(CONTAINER_NAME)...$(NC)"
	docker exec -it $(CONTAINER_NAME) /bin/bash

# Clean and rebuild (force rebuild without cache)
rebuild: clean
	@echo "$(GREEN)Rebuilding Docker image $(IMAGE_NAME):$(IMAGE_TAG) without cache...$(NC)"
	docker build --no-cache -f $(DOCKERFILE) -t $(IMAGE_NAME):$(IMAGE_TAG) .
	@echo "$(GREEN)Rebuild complete!$(NC)"

# Build and run in one command
up: build run

# Build and run the example module (minimal host - demo only)
run-example:
	@echo "$(GREEN)Building and running the example module (Minimal Host)...$(NC)"
	@echo "$(YELLOW)Step 1: Building the sample module Docker image...$(NC)"
	@docker build -f Dockerfile.example -t dotnetshell-example:latest .
	@echo "$(YELLOW)Step 2: Running the example module on port 5050...$(NC)"
	@echo "$(GREEN)=================================================================$(NC)"
	@echo "$(GREEN)The sample API will be available at:$(NC)"
	@echo "$(YELLOW)  - Main page: http://localhost:5050$(NC)"
	@echo "$(YELLOW)  - Swagger UI: http://localhost:5050/swagger$(NC)"
	@echo "$(YELLOW)  - Module info: http://localhost:5050/module-info$(NC)"
	@echo "$(RED)  - NOTE: No /health endpoints (minimal host demo)$(NC)"
	@echo "$(GREEN)=================================================================$(NC)"
	@echo "$(YELLOW)Press Ctrl+C to stop the application$(NC)"
	@docker run --rm -p 5050:5000 --name dotnetshell-example dotnetshell-example:latest

# Build and run with FULL shell (includes health endpoints)
run-shell:
	@echo "$(GREEN)Building and running with FULL DotNetShell.Host...$(NC)"
	@echo "$(YELLOW)This demonstrates the proper architecture with health endpoints from the shell$(NC)"
	@docker build -f Dockerfile.shell -t dotnetshell-full:latest .
	@echo "$(GREEN)=================================================================$(NC)"
	@echo "$(GREEN)The FULL Shell API will be available at:$(NC)"
	@echo "$(YELLOW)  - Main page: http://localhost:5000$(NC)"
	@echo "$(YELLOW)  - Health check: http://localhost:5000/health$(NC)"
	@echo "$(YELLOW)  - Liveness: http://localhost:5000/health/live$(NC)"
	@echo "$(YELLOW)  - Readiness: http://localhost:5000/health/ready$(NC)"
	@echo "$(YELLOW)  - Swagger UI: http://localhost:5000/swagger$(NC)"
	@echo "$(GREEN)=================================================================$(NC)"
	@echo "$(YELLOW)Press Ctrl+C to stop the application$(NC)"
	@docker run --rm -p 5000:5000 --name dotnetshell-full dotnetshell-full:latest

# Stop the running example
stop-example:
	@echo "$(YELLOW)Stopping example container...$(NC)"
	-docker stop dotnetshell-example 2>/dev/null || true
	@echo "$(GREEN)Example stopped.$(NC)"

# Clean up example artifacts
clean-example:
	@echo "$(YELLOW)Removing example Docker image...$(NC)"
	-docker rmi dotnetshell-example:latest 2>/dev/null || true
	-rm -f Dockerfile.example 2>/dev/null || true
	@echo "$(GREEN)Example artifacts cleaned.$(NC)"

# Check if Docker is installed and running
check-docker:
	@command -v docker >/dev/null 2>&1 || { echo "$(RED)Docker is not installed. Please install Docker first.$(NC)" >&2; exit 1; }
	@docker info >/dev/null 2>&1 || { echo "$(RED)Docker daemon is not running. Please start Docker.$(NC)" >&2; exit 1; }
	@echo "$(GREEN)Docker is installed and running.$(NC)"

# Build target with dependency check
build-with-check: check-docker build