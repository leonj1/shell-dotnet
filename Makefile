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
	@echo "  $(YELLOW)build$(NC)    - Build the Docker image using Dockerfile.build"
	@echo "  $(YELLOW)run$(NC)      - Run the container from the built image"
	@echo "  $(YELLOW)stop$(NC)     - Stop and remove the running container"
	@echo "  $(YELLOW)clean$(NC)    - Remove the Docker image"
	@echo "  $(YELLOW)logs$(NC)     - Show container logs"
	@echo "  $(YELLOW)shell$(NC)    - Open a shell in the running container"
	@echo "  $(YELLOW)rebuild$(NC)  - Clean and build the image (force rebuild)"

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

# Check if Docker is installed and running
check-docker:
	@command -v docker >/dev/null 2>&1 || { echo "$(RED)Docker is not installed. Please install Docker first.$(NC)" >&2; exit 1; }
	@docker info >/dev/null 2>&1 || { echo "$(RED)Docker daemon is not running. Please start Docker.$(NC)" >&2; exit 1; }
	@echo "$(GREEN)Docker is installed and running.$(NC)"

# Build target with dependency check
build-with-check: check-docker build