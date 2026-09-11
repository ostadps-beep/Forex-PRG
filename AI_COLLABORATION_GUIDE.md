# AI Collaboration Guide - Forex-P-A

## Project Purpose
This repository is an experimental analysis environment derived from ForexPanel-A.

## Reference
Baseline:
baseline-v1

Original project:
ForexPanel-A

## Rules
1. Work only inside Forex-P-A.
2. Do not modify ForexPanel-A directly.
3. Before major changes, analyze the existing architecture.
4. Keep commits small and descriptive.
5. Record reasons for important design decisions.

## Sensitive Areas
Review carefully before modifying:
- ChartController.cs
- MT4 communication layer
- Toolbar framework
- Theme system

## Workflow
Before changes:
git status

After changes:
git add .
git commit -m "description"
git push origin main

## Goal
Analyze, improve, and propose changes that can later be compared with the main project.