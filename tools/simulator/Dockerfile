FROM python:3.13-slim

WORKDIR /app

# Install dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy simulator source
COPY . .

# Default: run steady-state scenario
ENTRYPOINT ["python", "run.py"]
CMD ["--scenario", "steady-state"]
