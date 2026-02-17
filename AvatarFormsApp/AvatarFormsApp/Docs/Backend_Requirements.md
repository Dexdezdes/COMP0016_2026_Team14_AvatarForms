# Backend Python Requirements

## Create this file: `Backend\requirements.txt`

```txt
flask>=3.0.0
websockets>=12.0
python-dotenv>=1.0.0
```

## Install

```bash
cd Backend
pip install -r requirements.txt
```

## Or install individually

```bash
pip install flask websockets python-dotenv
```

## Verify Installation

```bash
python -c "import flask; print(f'Flask {flask.__version__} installed')"
python -c "import websockets; print(f'websockets installed')"
```

Should output:
```
Flask 3.0.x installed
websockets installed
```
