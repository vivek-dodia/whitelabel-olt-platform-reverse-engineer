FROM golang:1.25-alpine AS builder
WORKDIR /app
COPY go.mod ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 go build -ldflags="-s -w" -o grid-agent ./cmd/grid-agent

FROM alpine:3.20
RUN apk --no-cache add ca-certificates
WORKDIR /app
COPY --from=builder /app/grid-agent .
COPY config.json .
EXPOSE 8420
ENTRYPOINT ["./grid-agent"]
