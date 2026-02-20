FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy source code
COPY src/ ./src/
COPY notebooks/ ./notebooks/
COPY models/ ./models/

# Create directories for models and data
RUN mkdir -p /app/models /app/data

# Set Python path
ENV PYTHONPATH=/app

# Expose ports
EXPOSE 8000 8888

# Default command: run FastAPI ML service
# Use --workers 1 for now (single worker)
# For JupyterLab, override with: jupyter lab --ip=0.0.0.0 --port=8888 --no-browser --allow-root --NotebookApp.token=''
CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8000"]
